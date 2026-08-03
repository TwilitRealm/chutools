using System.Text;

using BmsDerg.Utility;

namespace BmsDerg;

public partial class Disassembler(BinaryReader reader, Document document)
{
    private uint _opStartPos;
    private readonly BinaryReader _reader = reader;
    private readonly Queue<uint> _disQueue = [];
    private readonly HashSet<uint> _hasDisassembled = [];
    private readonly Document _document = document;

    public void DisassembleFrom(uint entrypoint)
    {
        _disQueue.Enqueue(entrypoint);

        while (_disQueue.TryDequeue(out var entry))
        {
            DisassembleStraight(entry);
        }
    }

    private void DisassembleStraight(uint entrypoint)
    {
        if (!_hasDisassembled.Add(entrypoint))
            return;

        _reader.BaseStream.Position = entrypoint;

        while (true)
        {
            _opStartPos = (uint)_reader.BaseStream.Position;
            if (_document.Annotations.SearchOverlapping(new Interval<uint>(_opStartPos, _opStartPos + 1)).Count > 0)
            {
                break;
            }

            DisassembleResult result;
            var cmd = _reader.ReadByte();
            if ((cmd & 0x80) == 0)
            {
                result = DisNoteOn(cmd);
            }
            else
            {
                switch (cmd & 0xf0)
                {
                    case 0x80:
                        result = DisNoteOff((byte)(cmd & 0xF));
                        break;
                    case 0x90:
                        result = DisRegCommand((byte)((cmd & 7) + 1));
                        break;
                    default:
                        result = DisCommand(cmd, 0);
                        break;
                }
            }

            _document.Annotations.Insert(
                new OpcodeAnnotation(
                    new Interval<uint>(_opStartPos, (uint)_reader.BaseStream.Position),
                    result.Decoded));

            switch (result.Status)
            {
                case DisassembleResultStatus.Jump:
                    _reader.BaseStream.Position = result.JumpTarget;
                    break;
                case DisassembleResultStatus.Stop:
                    goto done;
            }
        }

        done: ;
    }

    private void QueueDis(uint entryPoint)
    {
        _disQueue.Enqueue(entryPoint);
    }

    private DisassembleResult DisRegCommand(byte val)
    {
        byte control = _reader.ReadByte();
        var origControl = control;
        ushort r29 = 0;
        ushort r28 = 3;
        for (int i = 0; i < val; i++)
        {
            if ((control & 0x80) != 0)
            {
                r29 |= r28;
            }

            control <<= 1;
            r28 <<= 2;
        }

        // _prefixCode = $"Reg.{val} 0x{origControl:X2} ";

        byte r25 = _reader.ReadByte();
        return DisCommand(r25, r29);
    }

    private DisassembleResult DisCommand(byte cmd, ushort val2)
    {
        OpcodeDef? def;
        if (cmd != 0xb0)
        {
            if (cmd < 0xA0)
                return DisassembleResult.Continue(NewContext(DefInvalid, []));

            def = Opcodes[cmd - 0xa0];
        }
        else
        {
            //cmdInfo = &sExtCmdInfo[seqCtrl->readByte() & 0xff];
            return DisassembleResult.Continue(NewContext(DefExt, []));
        }

        var parameterTypes = val2;

        var invalid = def == null;
        var args = new OpcodeArgument[def?.Parameters.Length ?? 0];
        for (var i = 0; i < args.Length; i++, parameterTypes >>= 2)
        {
            var paramType = def!.Parameters[i].Type;
            if ((parameterTypes & 3) == 3)
                paramType = OpcodeParamType.Register;

            var value = paramType switch
            {
                OpcodeParamType.Imm8 => _reader.ReadByte(),
                OpcodeParamType.Imm16 => _reader.ReadUInt16BE(),
                OpcodeParamType.Imm24 or OpcodeParamType.CodePointer => _reader.ReadUInt24BE(),
                OpcodeParamType.Register => _reader.ReadByte(),
                _ => throw new ArgumentOutOfRangeException()
            };

            var argType = paramType == OpcodeParamType.Register
                ? OpcodeArgumentType.Register
                : OpcodeArgumentType.Immediate;

            if (paramType == OpcodeParamType.CodePointer)
                QueueDis(value);

            args[i] = new OpcodeArgument { Type = argType, Value = value };
        }

        if (invalid)
        {
            return DisassembleResult.Continue(NewContext(DefInvalid, []));
        }

        var ctx = NewContext(def!, args);
        var handler = def!.Handler ?? DisDefault;

        return handler(in ctx);
    }

    private OpcodeContext NewContext(OpcodeDef def, OpcodeArgument[] args)
    {
        return new OpcodeContext { Dis = this, Args = args, Def = def };
    }

    public record struct DisassembleResult(DisassembleResultStatus Status, uint JumpTarget, DecodedOpcode Decoded)
    {
        public static DisassembleResult Jump(DecodedOpcode decoded, uint target) =>
            new(DisassembleResultStatus.Stop, target, decoded);

        public static DisassembleResult Jump(in OpcodeContext ctx, uint target) =>
            new(DisassembleResultStatus.Stop, target, new DecodedOpcode(ctx));

        public static DisassembleResult Continue(DecodedOpcode decoded) =>
            new(DisassembleResultStatus.Continue, 0, decoded);

        public static DisassembleResult Continue(in OpcodeContext ctx) =>
            new(DisassembleResultStatus.Continue, 0, new DecodedOpcode(ctx));

        public static DisassembleResult Stop(DecodedOpcode decoded) =>
            new(DisassembleResultStatus.Stop, 0, decoded);

        public static DisassembleResult Stop(in OpcodeContext ctx) =>
            new(DisassembleResultStatus.Stop, 0, new DecodedOpcode(ctx));
    }

    public enum DisassembleResultStatus : byte
    {
        Continue,
        Stop,
        Jump
    }
}