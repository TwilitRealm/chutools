using System.Collections.Immutable;
using System.Diagnostics;

using BmsDerg.Utility;

namespace BmsDerg;

public partial class Disassembler
{
    private static readonly OpcodeDef DefInvalid = new("!!INVALID!!", []);
    private static readonly OpcodeDef DefExt = new("!!EXT!!", []);
    private static readonly OpcodeDef DefNoteOn = new("NoteOn", [Imm8(), Imm8(), Imm8()]);
    private static readonly OpcodeDef DefNoteOnMidi = new("NoteOn", [Imm8(), Imm8(), Imm8(), Imm24()]);
    private static readonly OpcodeDef DefNoteOff = new("NoteOff", [Imm8()]);

    private static readonly OpcodeDef?[] Opcodes =
    [
        null,
        null,
        null,
        null,
        null,
        null,
        null,
        null,
        null,
        null,
        null,
        null,
        null,
        null,
        null,
        null,
        null,
        new("NoteOnCmd", 0x0003, 0x0000),
        new("NoteOffCmd", 0x0001, 0x0000),
        new("Note", 0x0004, 0x0040),
        new("SetLastNote", 0x0001, 0x0000),
        null,
        null,
        null,
        new("ParamE", 0x0002, 0x0000),
        new("ParamI", 0x0002, 0x0004),
        new("ParamEI", 0x0003, 0x0010),
        new("ParamII", 0x0003, 0x0014),
        null,
        null,
        null,
        null,
        null,
        new("OpenTrack", [Imm8(), CodePointer("Target")]),
        new("CloseTrack", 0x0001, 0x0000),
        new("Call", [CodePointer("Target")]),
        new("CallF", 0x0002, 0x0008),
        new("Ret", DisRet, 0x0000, 0x0000),
        new("RetF", 0x0001, 0x0000),
        new("Jmp", DisJmp, [CodePointer("Target")]),
        new("JmpF", [Imm8("Condition"), CodePointer("Target")]),
        new("JmpTable", DisJmpTable, [Reg(), Imm24().Xref()]),
        new("CallTable", [Reg(), Imm24().Xref()]),
        new("LoopS", 0x0001, 0x0001),
        new("LoopE", 0x0000, 0x0000),
        null,
        null,
        null,
        new("ReadPort", [Imm8("Port"), Imm8("Register").RegId()]),
        new("WritePort", 0x0002, 0x000C),
        new("CheckPortImport", 0x0001, 0x0000),
        new("CheckPortExport", 0x0001, 0x0000),
        new("ParentWritePort", 0x0002, 0x000C),
        new("ChildWritePort", 0x0002, 0x000C),
        new("ParentReadPort", 0x0002, 0x0000),
        new("ChildReadPort", 0x0002, 0x0000),
        new("RegLoad", [Imm8("Register").RegId(), Imm16("Value")]),
        new("Reg", [Imm8("Operation"), Imm8().RegId(), Reg("InputValue")]),
        new("Reg", [Imm8("Operation"), Imm8().RegId(), Imm16("InputValue")]),
        new("RegUni", 0x0002, 0x0000),
        null, // TODO: new(&JASSeqParser::cmdRegTblLoad, 0x0004, 0x00E0),
        null,
        null,
        null,
        new("Tempo", 0x0001, 0x0001),
        new("BankPrg", 0x0001, 0x0001),
        new("Bank", 0x0001, 0x0000),
        new("Prg", 0x0001, 0x0000),
        null,
        null,
        null,
        new("EnvScaleSet", 0x0002, 0x0004),
        new("EnvSet", 0x0002, 0x0008),
        new("SimpleADSR", 0x0005, 0x0155),
        new("BusConnect", 0x0002, 0x0004),
        new("IIRCutOff", 0x0001, 0x0000),
        new("IIRSet", 0x0004, 0x0055),
        new("FIRSet", 0x0001, 0x0002),
        null,
        null,
        new("Wait", DisWait, 0x0000, 0x0000),
        new("WaitByte", 0x0001, 0x0000),
        null,
        new("SetIntTable", 0x0001, 0x0002),
        new("SetInterrupt", 0x0001, 0x0001),
        new("DisInterrupt", 0x0001, 0x0001),
        new("RetI", 0x0000, 0x0000),
        new("ClrI", 0x0000, 0x0000),
        new("IntTimer", 0x0002, 0x0004),
        new("SyncCPU", 0x0001, 0x0001),
        null,
        null,
        null,
        null, // TODO: new(&JASSeqParser::cmdPrintf, 0x0000, 0x0000),
        new("Nop", 0x0000, 0x0000),
        new("Finish", DisFinish, 0x0000, 0x0000),
    ];

    private DisassembleResult DisNoteOn(byte cmd)
    {
        var voice = _reader.ReadByte();
        var velocity = _reader.ReadByte();

        if (voice == 0)
        {
            var midi = _reader.ReadVarInt32();
            var ctx = NewContext(DefNoteOnMidi,
            [
                OpcodeArgument.Immediate(cmd), OpcodeArgument.Immediate(voice), OpcodeArgument.Immediate(velocity),
                OpcodeArgument.Immediate((uint)midi)
            ]);

            return DisassembleResult.Continue(ctx);
        }
        else
        {
            var ctx = NewContext(DefNoteOn,
            [
                OpcodeArgument.Immediate(cmd), OpcodeArgument.Immediate(voice), OpcodeArgument.Immediate(velocity)
            ]);
            return DisassembleResult.Continue(ctx);
        }
    }

    private DisassembleResult DisNoteOff(byte nibble)
    {
        var newCtx = new OpcodeContext
        {
            Dis = this, Def = DefNoteOff, Args = [OpcodeArgument.Immediate((uint)(nibble & 0x8))]
        };
        return DisassembleResult.Continue(newCtx);
    }

    private static DisassembleResult DisRet(in OpcodeContext ctx)
    {
        return DisassembleResult.Stop(ctx);
    }

    private static DisassembleResult DisFinish(in OpcodeContext ctx)
    {
        return DisassembleResult.Stop(ctx);
    }

    private static DisassembleResult DisJmp(in OpcodeContext ctx)
    {
        if (ctx.Args[0].Type == OpcodeArgumentType.Immediate)
        {
            return DisassembleResult.Jump(ctx, ctx.Args[0].Value);
        }
        else
        {
            return DisassembleResult.Stop(ctx);
        }
    }

    private static DisassembleResult DisJmpTable(in OpcodeContext ctx)
    {
        if (ctx.Args[1] is { Type: OpcodeArgumentType.Immediate, Value: { } val })
        {
            Interval<uint> interval = new(val, val + 3u);
            if (ctx.Dis._document.Annotations.SearchOverlapping(interval).Count == 0)
            {
                ctx.Dis._document.Annotations.Insert(new JumpTableAnnotation(interval));
            }
        }

        return DisassembleResult.Stop(ctx);
    }

    private static DisassembleResult DisWait(in OpcodeContext ctx)
    {
        var val = ctx.Dis._reader.ReadVarInt32();
        return DisassembleResult.Continue(ctx.CustomArgs(new OpcodeDef("Wait", [Imm24()]),
            [OpcodeArgument.Immediate((uint)val)]));
    }

    private static DisassembleResult DisDefault(in OpcodeContext ctx)
    {
        return DisassembleResult.Continue(ctx);
    }

    public delegate DisassembleResult DisassemblerFunc(in OpcodeContext context);

    public sealed class OpcodeDef
    {
        public string Name { get; }
        public DisassemblerFunc? Handler { get; }
        public ImmutableArray<OpcodeParameter> Parameters { get; }

        public OpcodeDef(string name, ImmutableArray<OpcodeParameter> parameters)
        {
            Name = name;
            Parameters = parameters;
        }

        public OpcodeDef(string name, uint argCount, uint argTypes)
        {
            Name = name;
            Parameters = FromCodeDef(argCount, argTypes);
        }

        public OpcodeDef(string name, DisassemblerFunc handler, uint argCount, uint argTypes)
        {
            Name = name;
            Handler = handler;
            Parameters = FromCodeDef(argCount, argTypes);
        }

        public OpcodeDef(string name, DisassemblerFunc handler, ImmutableArray<OpcodeParameter> parameters)
        {
            Name = name;
            Handler = handler;
            Parameters = parameters;
        }

        private static ImmutableArray<OpcodeParameter> FromCodeDef(uint argCount, uint argTypes)
        {
            var arr = new OpcodeParameter[argCount];
            for (var i = 0; i < argCount; i++, argTypes >>= 2)
            {
                var param = (argTypes & 3) switch
                {
                    0 => Imm8(),
                    1 => Imm16(),
                    2 => Imm24(),
                    3 => Reg(),
                    _ => throw new UnreachableException()
                };

                arr[i] = param;
            }

            return [..arr];
        }
    }

    public struct OpcodeParameter
    {
        public string? Name;
        public OpcodeParamType Type;
        public OpcodeFormat Format;

        public OpcodeParameter WithFormat(OpcodeFormat fmt)
        {
            var copy = this;
            copy.Format = fmt;
            return copy;
        }

        public OpcodeParameter Xref()
        {
            return WithFormat(OpcodeFormat.Xref);
        }

        public OpcodeParameter RegId()
        {
            return WithFormat(OpcodeFormat.RegId);
        }
    }

    private static OpcodeParameter Imm8(string? name = null)
    {
        return new OpcodeParameter { Name = name, Type = OpcodeParamType.Imm8 };
    }

    private static OpcodeParameter Imm16(string? name = null)
    {
        return new OpcodeParameter { Name = name, Type = OpcodeParamType.Imm16 };
    }

    private static OpcodeParameter Imm24(string? name = null)
    {
        return new OpcodeParameter { Name = name, Type = OpcodeParamType.Imm24 };
    }

    private static OpcodeParameter CodePointer(string? name = null)
    {
        return new OpcodeParameter { Name = name, Type = OpcodeParamType.CodePointer, Format = OpcodeFormat.Xref, };
    }

    private static OpcodeParameter Reg(string? name = null)
    {
        return new OpcodeParameter { Name = name, Type = OpcodeParamType.Register };
    }

    public struct OpcodeArgument : IFormattable
    {
        public OpcodeArgumentType Type;
        public uint Value;

        public override string ToString()
        {
            return ToString(null, null);
        }

        public string ToString(string? format, IFormatProvider? formatProvider)
        {
            if (Type == OpcodeArgumentType.Immediate)
            {
                return Value.ToString(format, formatProvider);
            }
            else
            {
                return $"r{Value}";
            }
        }

        public static OpcodeArgument Immediate(uint value)
        {
            return new OpcodeArgument { Type = OpcodeArgumentType.Immediate, Value = value };
        }
    }

    public ref struct OpcodeContext
    {
        public Disassembler Dis;
        public OpcodeArgument[] Args;
        public OpcodeDef Def;

        public readonly OpcodeContext CustomArgs(OpcodeDef newDef, OpcodeArgument[] args)
        {
            var copy = this;
            copy.Def = newDef;
            copy.Args = args;
            return copy;
        }
    }

    public enum OpcodeArgumentType : byte
    {
        Immediate,
        Register,
    }

    public enum OpcodeParamType : byte
    {
        Imm8,
        Imm16,
        Imm24,
        CodePointer,
        Register,
    }

    public enum OpcodeFormat : byte
    {
        Nome,
        Xref,
        RegId
    }
}