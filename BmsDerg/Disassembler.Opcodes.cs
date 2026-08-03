using System.Collections.Immutable;
using System.Diagnostics;

using BmsDerg.Utility;

namespace BmsDerg;

public partial class Disassembler
{
    private static readonly OpcodeDef[] Opcodes =
    [
        default,
        default,
        default,
        default,
        default,
        default,
        default,
        default,
        default,
        default,
        default,
        default,
        default,
        default,
        default,
        default,
        default,
        new("NoteOnCmd", 0x0003, 0x0000),
        new("NoteOffCmd", 0x0001, 0x0000),
        new("Note", 0x0004, 0x0040),
        new("SetLastNote", 0x0001, 0x0000),
        default,
        default,
        default,
        new("ParamE", 0x0002, 0x0000),
        new("ParamI", 0x0002, 0x0004),
        new("ParamEI", 0x0003, 0x0010),
        new("ParamII", 0x0003, 0x0014),
        default,
        default,
        default,
        default,
        default,
        new("OpenTrack", [Imm8(), CodePointer("Target")]),
        new("CloseTrack", 0x0001, 0x0000),
        new("Call", 0x0001, 0x0002),
        new("CallF", 0x0002, 0x0008),
        new(DisRet, 0x0000, 0x0000),
        new("RetF", 0x0001, 0x0000),
        new(DisJmp, [CodePointer("Target")]),
        new("JmpF", [Imm8("Condition"), CodePointer("Target")]),
        new(DisJmpTable, [Reg(), Imm24().Xref()]),
        new("CallTable", [Reg(), Imm24().Xref()]),
        new("LoopS", 0x0001, 0x0001),
        new("LoopE", 0x0000, 0x0000),
        default,
        default,
        default,
        new("ReadPort", [Imm8("Port"), Imm8("Register").WithFormat(OpcodeFormat.OutReg)]),
        new("WritePort", 0x0002, 0x000C),
        new("CheckPortImport", 0x0001, 0x0000),
        new("CheckPortExport", 0x0001, 0x0000),
        new("ParentWritePort", 0x0002, 0x000C),
        new("ChildWritePort", 0x0002, 0x000C),
        new("ParentReadPort", 0x0002, 0x0000),
        new("ChildReadPort", 0x0002, 0x0000),
        new("RegLoad", [Imm8("Register").WithFormat(OpcodeFormat.OutReg), Imm16("Value")]),
        new("Reg", 0x0003, 0x0030),
        new("Reg", 0x0003, 0x0010),
        new("RegUni", 0x0002, 0x0000),
        default, // TODO: new(&JASSeqParser::cmdRegTblLoad, 0x0004, 0x00E0),
        default,
        default,
        default,
        new("Tempo", 0x0001, 0x0001),
        new("BankPrg", 0x0001, 0x0001),
        new("Bank", 0x0001, 0x0000),
        new("Prg", 0x0001, 0x0000),
        default,
        default,
        default,
        new("EnvScaleSet", 0x0002, 0x0004),
        new("EnvSet", 0x0002, 0x0008),
        new("SimpleADSR", 0x0005, 0x0155),
        new("BusConnect", 0x0002, 0x0004),
        new("IIRCutOff", 0x0001, 0x0000),
        new("IIRSet", 0x0004, 0x0055),
        new("FIRSet", 0x0001, 0x0002),
        default,
        default,
        new(DisWait, 0x0000, 0x0000),
        new("WaitByte", 0x0001, 0x0000),
        default,
        new("SetIntTable", 0x0001, 0x0002),
        new("SetInterrupt", 0x0001, 0x0001),
        new("DisInterrupt", 0x0001, 0x0001),
        new("RetI", 0x0000, 0x0000),
        new("ClrI", 0x0000, 0x0000),
        new("IntTimer", 0x0002, 0x0004),
        new("SyncCPU", 0x0001, 0x0001),
        default,
        default,
        default,
        default, // TODO: new(&JASSeqParser::cmdPrintf, 0x0000, 0x0000),
        new("Nop", 0x0000, 0x0000),
        new(DisFinish, 0x0000, 0x0000),
    ];

    private DisassembleResult DisNoteOn(byte cmd)
    {
        var voice = _reader.ReadByte();
        var velocity = _reader.ReadByte();

        if (voice == 0)
        {
            var midi = _reader.ReadVarInt32();
            return DisassembleResult.Continue($"NoteOn {cmd},{voice},{velocity},{midi}");
        }
        else
        {
            return DisassembleResult.Continue($"NoteOn {cmd},{voice},{velocity}");
        }
    }

    private static DisassembleResult DisNoteOff(byte nibble)
    {
        return DisassembleResult.Continue($"NoteOff {nibble & 0x8}");
    }

    private static DisassembleResult DisRet(in OpcodeContext ctx)
    {
        return DisassembleResult.Stop(DefaultDisassemble("Ret", ctx));
    }

    private static DisassembleResult DisFinish(in OpcodeContext ctx)
    {
        return DisassembleResult.Stop(DefaultDisassemble("Finish", ctx));
    }

    private static DisassembleResult DisJmp(in OpcodeContext ctx)
    {
        string decoded = DefaultDisassemble("Jmp", ctx);

        if (ctx.Args[0].Type == OpcodeArgumentType.Immediate)
        {
            return DisassembleResult.Jump(decoded, ctx.Args[0].Value);
        }
        else
        {
            return DisassembleResult.Stop(decoded);
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

        string decoded = DefaultDisassemble("JmpTable", ctx);

        return DisassembleResult.Stop(decoded);
    }

    private static DisassembleResult DisWait(in OpcodeContext ctx)
    {
        var val = ctx.Dis._reader.ReadVarInt32();
        return DisassembleResult.Continue($"Wait {val}");
    }

    private delegate DisassembleResult DisassemblerFunc(in OpcodeContext context);

    private struct OpcodeDef
    {
        public string? Name { get; }
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

        public OpcodeDef(DisassemblerFunc handler, uint argCount, uint argTypes)
        {
            Handler = handler;
            Parameters = FromCodeDef(argCount, argTypes);
        }

        public OpcodeDef(DisassemblerFunc handler, ImmutableArray<OpcodeParameter> parameters)
        {
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

    private struct OpcodeParameter
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
    }

    private static OpcodeParameter Imm8(string? name = null)
    {
        return new OpcodeParameter
        {
            Name = name,
            Type = OpcodeParamType.Imm8
        };
    }

    private static OpcodeParameter Imm16(string? name = null)
    {
        return new OpcodeParameter
        {
            Name = name,
            Type = OpcodeParamType.Imm16
        };
    }

    private static OpcodeParameter Imm24(string? name = null)
    {
        return new OpcodeParameter
        {
            Name = name,
            Type = OpcodeParamType.Imm24
        };
    }

    private static OpcodeParameter CodePointer(string? name = null)
    {
        return new OpcodeParameter
        {
            Name = name,
            Type = OpcodeParamType.CodePointer,
            Format = OpcodeFormat.Xref,
        };
    }

    private static OpcodeParameter Reg(string? name = null)
    {
        return new OpcodeParameter
        {
            Name = name,
            Type = OpcodeParamType.Register
        };
    }

    private struct OpcodeArgument : IFormattable
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
    }

    private ref struct OpcodeContext
    {
        public Disassembler Dis;
        public OpcodeArgument[] Args;
        public OpcodeDef Def;
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
        OutReg
    }
}
