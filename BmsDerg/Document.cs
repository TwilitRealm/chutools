using System.Runtime.InteropServices;

using BmsDerg.Utility;

namespace BmsDerg;

public abstract class BaseDocumentAnnotation : INodeInterval<uint>
{
    public Interval<uint> Interval { get; }

    protected BaseDocumentAnnotation(Interval<uint> interval)
    {
        Interval = interval;
    }
}

public sealed class OpcodeAnnotation : BaseDocumentAnnotation
{
    public DecodedOpcode Decoded { get; }
    public Disassembler.DisassembleResultStatus Status { get; }

    public OpcodeAnnotation(Interval<uint> interval, DecodedOpcode decoded, Disassembler.DisassembleResultStatus status) : base(interval)
    {
        Decoded = decoded;
        Status = status;
    }
}

public interface ITableAnnotation : INodeInterval<uint>
{
    uint Step { get; }
    BaseDocumentAnnotation Expanded(Interval<uint> newInterval);
}

public sealed class JumpTableAnnotation : BaseDocumentAnnotation, ITableAnnotation
{
    public JumpTableAnnotation(Interval<uint> interval) : base(interval)
    {
    }

    public uint Step => 3;

    public BaseDocumentAnnotation Expanded(Interval<uint> newInterval)
    {
        return new JumpTableAnnotation(newInterval);
    }
}

public enum RegTableDataType
{
    U8,
    U16,
    U24,
    U32,
}

public sealed class RegTableAnnotation : BaseDocumentAnnotation, ITableAnnotation
{
    public RegTableDataType DataType { get; }

    public RegTableAnnotation(RegTableDataType dataType, Interval<uint> interval) : base(interval)
    {
        DataType = dataType;
    }

    public uint Step => DataType switch
    {
        RegTableDataType.U8 => 1,
        RegTableDataType.U16 => 2,
        RegTableDataType.U24 => 3,
        RegTableDataType.U32 => 4,
        _ => throw new ArgumentOutOfRangeException()
    };

    public BaseDocumentAnnotation Expanded(Interval<uint> newInterval)
    {
        return new RegTableAnnotation(DataType, newInterval);
    }
}

public sealed class SectionAnnotation : BaseDocumentAnnotation
{
    public string Name { get; }

    public SectionAnnotation(Interval<uint> interval, string name) : base(interval)
    {
        Name = name;
    }
}


public sealed class DecodedOpcode
{
    public Disassembler.OpcodeDef Opcode { get; }
    public Disassembler.OpcodeArgument[] Arguments { get; }
    public string? Explanation { get; }

    public DecodedOpcode(Disassembler.OpcodeDef opcode, Disassembler.OpcodeArgument[] arguments, string? explanation = null)
    {
        Opcode = opcode;
        Arguments = arguments;
        Explanation = explanation;
    }

    public DecodedOpcode(in Disassembler.OpcodeContext context, string? explanation = null) : this(context.Def, context.Args, explanation)
    {

    }
}

public enum XrefType : byte
{
    EntryPoint,
    Xref,
}

public readonly record struct Xref(XrefType Type, byte Category, uint Idx)
{
    public static Xref FromEntry(int category, int idx) => new(XrefType.EntryPoint, (byte)category, (uint)idx);
    public static Xref FromXref(uint address) => new(XrefType.Xref, 0, address);
}

public sealed class Document
{
    public byte[] Data { get; }
    public AvlTree<uint, BaseDocumentAnnotation> Annotations { get; } = new();
    public Dictionary<uint, HashSet<Xref>> Xrefs { get; } = new();

    public Document(byte[] data)
    {
        Data = data;
    }

    public void InsertXref(uint target, Xref xref)
    {
        ref var list = ref CollectionsMarshal.GetValueRefOrAddDefault(Xrefs, target, out _);
        list ??= [];
        list.Add(xref);
    }
}
