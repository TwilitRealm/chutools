using System.Collections.Immutable;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;

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
        new("CallF", DisCallF, [Imm8("Condition"), CodePointer("Target")]),
        new("Ret", DisRet, 0x0000, 0x0000),
        new("RetF", DisRetF, [Imm8("Condition")]),
        new("Jmp", DisJmp, [CodePointer("Target")]),
        new("JmpF", DisJmpF, [Imm8("Condition"), CodePointer("Target")]),
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
        new("RegLoad", DisRegLoad, [Imm8("Register").RegId(), Imm16("Value")]),
        new("Reg", DisReg, [Imm8("Operation"), Imm8().RegId(), Reg("InputValue")]),
        new("Reg", DisReg, [Imm8("Operation"), Imm8().RegId(), Imm16("InputValue")]),
        new("RegUni", 0x0002, 0x0000),
        new("RegTblLoad", DisRegTblLoad, [Imm8("Operation"), Imm8().RegId(), Imm24("Table").Xref(), Reg("Offset")]),
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
        new("Printf", DisPrintf, []),
        new("Nop", 0x0000, 0x0000),
        new("Finish", DisFinish, 0x0000, 0x0000),
    ];

    private DisassembleResult DisNoteOn(byte cmd)
    {
        var voice = _reader.ReadByte();
        var velocity = _reader.ReadByte();

        var voiceMasked = (byte)(voice & 7);
        if (voiceMasked == 0)
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
                OpcodeArgument.Immediate(cmd), OpcodeArgument.Immediate(voiceMasked), OpcodeArgument.Immediate(velocity)
            ]);
            return DisassembleResult.Continue(ctx);
        }
    }

    private DisassembleResult DisNoteOff(byte nibble)
    {
        var newCtx = new OpcodeContext
        {
            Dis = this, Def = DefNoteOff, Args = [OpcodeArgument.Immediate((uint)(nibble & 0x7))]
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

            ctx.Dis.InsertXrefFromCurrentOpcode(val);
        }

        return DisassembleResult.Stop(ctx);
    }

    private static DisassembleResult DisWait(in OpcodeContext ctx)
    {
        var val = ctx.Dis._reader.ReadVarInt32();
        return DisassembleResult.Continue(ctx.CustomArgs(new OpcodeDef("Wait", [Imm24()]),
            [OpcodeArgument.Immediate((uint)val)]));
    }

    private static DisassembleResult DisReg(in OpcodeContext ctx)
    {
        string? explanation = null;

        if (ctx.Args[0].Type == OpcodeArgumentType.Immediate)
        {
            explanation = ctx.Args[0].Value switch
            {
                0 => $"{RegIds.RegIdArg(ctx.Args[1])} = {ctx.Args[2]}",
                1 => $"{RegIds.RegIdArg(ctx.Args[1])} += {ctx.Args[2]}",
                2 => $"{RegIds.RegIdArg(ctx.Args[1])} -= {ctx.Args[2]}",
                3 => $"{RegIds.R3} = {RegIds.RegIdArg(ctx.Args[1])} - {ctx.Args[2]}",
                4 => $"{RegIds.GetRegName(0x21)} = {RegIds.RegIdArg(ctx.Args[1])} * {ctx.Args[2]}",
                5 => $"{RegIds.RegIdArg(ctx.Args[1])} &= {ctx.Args[2]}",
                6 => $"{RegIds.RegIdArg(ctx.Args[1])} |= {ctx.Args[2]}",
                7 => $"{RegIds.RegIdArg(ctx.Args[1])} ^= {ctx.Args[2]}",
                8 => $"{RegIds.RegIdArg(ctx.Args[1])} = rand({ctx.Args[2]})",
                _ => null
            };
        }

        return DisassembleResult.Continue(new DecodedOpcode(ctx, explanation));
    }

    private static DisassembleResult DisRegLoad(in OpcodeContext ctx)
    {
        var explanation = $"{RegIds.RegIdArg(ctx.Args[0])} = {ctx.Args[1]}";

        return DisassembleResult.Continue(new DecodedOpcode(ctx, explanation));
    }

    private static DisassembleResult DisRegTblLoad(in OpcodeContext ctx)
    {
        RegTableDataType? dataType = null;
        string? explanation = null;
        if (ctx.Args[0] is { Type: OpcodeArgumentType.Immediate, Value: var cmd })
        {
            (dataType, explanation) = cmd switch
            {
                12 => (RegTableDataType.U8, $"= ((u8*){ctx.Args[2]})[{ctx.Args[3]}]"),
                13 => (RegTableDataType.U16, $"= ((u16*){ctx.Args[2]})[{ctx.Args[3]}]"),
                14 => (RegTableDataType.U24, $"= ((u24*){ctx.Args[2]})[{ctx.Args[3]}]"),
                15 => (RegTableDataType.U32, $"= ((u32*){ctx.Args[2]})[{ctx.Args[3]}]"),
                16 => (RegTableDataType.U32, $"= *(u32*)({ctx.Args[2]} + {ctx.Args[3]})"),
            };
        }

        if (dataType != null && ctx.Args[2] is { Type: OpcodeArgumentType.Immediate, Value: var val })
        {
            Interval<uint> interval = new(val, val + 3u);
            if (ctx.Dis._document.Annotations.SearchOverlapping(interval).Count == 0)
            {
                ctx.Dis._document.Annotations.Insert(new RegTableAnnotation(dataType.Value, interval));
            }

            ctx.Dis.InsertXrefFromCurrentOpcode(val);
        }

        DecodedOpcode opcode = explanation != null
            ? new DecodedOpcode(ctx, $"{RegIds.RegIdArg(ctx.Args[1])} {explanation}")
            : new DecodedOpcode(ctx);

        return DisassembleResult.Continue(opcode);
    }

    private static DisassembleResult DisJmpF(in OpcodeContext ctx)
    {
        return DescribeCondition(ctx, "jump");
    }

    private static DisassembleResult DisCallF(in OpcodeContext ctx)
    {
        return DescribeCondition(ctx, "call");
    }

    private static DisassembleResult DisRetF(in OpcodeContext ctx)
    {
        return DescribeCondition(ctx, "return");
    }

    private static DisassembleResult DescribeCondition(in OpcodeContext ctx, string verb)
    {
        if (ctx.Args[0].Type != OpcodeArgumentType.Immediate)
            return DisassembleResult.Continue(ctx);

        var explanation = ctx.Args[0].Value switch
        {
            0 => "always",
            1 => "if r3 == 0",
            2 => "if r3 != 0",
            3 => "if r3 == 1",
            4 => "if r3 < 0",
            5 => "if r3 > 0",
        };

        explanation = $"{verb} {explanation}";
        return DisassembleResult.Continue(new DecodedOpcode(ctx, explanation));
    }

    private static DisassembleResult DisPrintf(in OpcodeContext ctx)
    {
        var buf = new List<byte>();
        while (true)
        {
            var val = ctx.Dis._reader.ReadByte();
            if (val == 0)
                break;

            buf.Add(val);
        }

        var str = Encoding.ASCII.GetString(CollectionsMarshal.AsSpan(buf));
        return DisassembleResult.Continue(new DecodedOpcode(ctx, $"\"{str}\""));
    }

    private static DisassembleResult DisDefault(in OpcodeContext ctx)
    {
        return DisassembleResult.Continue(ctx);
    }

    public delegate DisassembleResult DisassemblerFunc(in OpcodeContext context);

    [DebuggerDisplay("Opcode {Name}")]
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
                return RegIds.GetRegName(Value);
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