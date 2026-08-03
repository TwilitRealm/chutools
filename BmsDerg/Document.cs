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

    public OpcodeAnnotation(Interval<uint> interval, DecodedOpcode decoded) : base(interval)
    {
        Decoded = decoded;
    }
}

public sealed class JumpTableAnnotation : BaseDocumentAnnotation
{
    public JumpTableAnnotation(Interval<uint> interval) : base(interval)
    {
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

    public DecodedOpcode(Disassembler.OpcodeDef opcode, Disassembler.OpcodeArgument[] arguments)
    {
        Opcode = opcode;
        Arguments = arguments;
    }

    public DecodedOpcode(in Disassembler.OpcodeContext context) : this(context.Def, context.Args)
    {

    }
}

public sealed class Document
{
    public byte[] Data { get; }
    public AvlTree<uint, BaseDocumentAnnotation> Annotations { get; } = new();
    public Dictionary<uint, List<(int category, int idx)>> EntryPoints { get; } = new();

    public Document(byte[] data)
    {
        Data = data;
    }
}
