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
            if (_opStartPos == 0x0089CE)
            {

            }
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
        OpcodeDef def;
        if (cmd != 0xb0)
        {
            if (cmd < 0xA0)
                return DisassembleResult.Continue("INVALID");

            def = Opcodes[cmd - 0xa0];
        }
        else
        {
            //cmdInfo = &sExtCmdInfo[seqCtrl->readByte() & 0xff];
            return DisassembleResult.Continue("EXT");
        }

        var parameterTypes = val2;

        var invalid = def.Name == null && def.Handler == null;
        var args = new OpcodeArgument[invalid ? 0 : def.Parameters.Length];
        for (var i = 0; i < args.Length; i++, parameterTypes >>= 2)
        {
            var paramType = def.Parameters[i].Type;
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
            return DisassembleResult.Continue("INVALID");
        }

        var ctx = new OpcodeContext { Dis = this, Args = args, Def = def };

        if (def.Name != null)
        {
            var decoded = DefaultDisassemble(def.Name, ctx);
            return DisassembleResult.Continue(decoded);
        }
        else
        {
            return def.Handler!(ctx);
        }
    }

    private static string DefaultDisassemble(string name, in OpcodeContext context)
    {
        var sb = new StringBuilder();
        sb.Append(name);
        sb.Append(' ');

        for (var i = 0; i < context.Args.Length; i++)
        {
            if (i != 0)
                sb.Append(',');

            var arg = context.Args[i];
            var param = context.Def.Parameters[i];

            string? fmt = null;
            if (param.Format == OpcodeFormat.OutReg)
            {
                sb.Append('r');
            }
            else if (param.Format == OpcodeFormat.Xref)
            {
                fmt = "X06";
                sb.Append("0x");
            }

            sb.Append(arg.ToString(fmt, null));
        }

        return sb.ToString();
    }

    /*
    private void WriteOpcode(string opcode)
    {
        WriteOpcodePreamble();
        if (_prefixCode != null)
        {
            _writer.Write(_prefixCode);
            _prefixCode = null;
        }

        _writer.WriteLine(opcode);
    }

    private void WriteOpcodePreamble()
    {
        if (_entryPoints.TryGetValue(_opStartPos, out var sound))
        {
            foreach (var entry in sound)
            {
                _writer.WriteLine($"; ENTRY: {entry.category}, 0x{entry.idx:X04}");
            }
        }

        _writer.Write($"{_opStartPos:X6}  ");

        var bytes = "";

        var endPos = _reader.BaseStream.Position;
        _reader.BaseStream.Position = _opStartPos;

        while (_reader.BaseStream.Position < endPos)
        {
            bytes += $"{_reader.ReadByte():X2} ";
        }

        _reader.BaseStream.Position = endPos;

        _writer.Write("{0,-20}", bytes);
    }
    */

    private record struct DisassembleResult(DisassembleResultStatus Status, uint JumpTarget, DecodedOpcode Decoded)
    {
        public static DisassembleResult Jump(DecodedOpcode decoded, uint target) =>
            new(DisassembleResultStatus.Stop, 0, decoded);

        public static DisassembleResult Jump(string decoded, uint target) =>
            new(DisassembleResultStatus.Stop, 0, new DecodedOpcode(decoded));

        public static DisassembleResult Continue(DecodedOpcode decoded) =>
            new(DisassembleResultStatus.Continue, 0, decoded);

        public static DisassembleResult Continue(string decoded) =>
            new(DisassembleResultStatus.Continue, 0, new DecodedOpcode(decoded));

        public static DisassembleResult Stop(DecodedOpcode decoded) =>
            new(DisassembleResultStatus.Stop, 0, decoded);

        public static DisassembleResult Stop(string decoded) =>
            new(DisassembleResultStatus.Stop, 0, new DecodedOpcode(decoded));
    }

    private enum DisassembleResultStatus : byte
    {
        Continue,
        Stop,
        Jump
    }
}