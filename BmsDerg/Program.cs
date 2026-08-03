using System.CommandLine;
using System.Runtime.InteropServices;

using Be.IO;

using BmsDerg;
using BmsDerg.Utility;

using xayrga.bast;

var argFile = new Argument<FileInfo>("input");
var argOutput = new Argument<FileInfo>("output");
var argBstn = new Option<FileInfo>("--bstn");

var rootCmd = new RootCommand();
rootCmd.Arguments.Add(argFile);
rootCmd.Arguments.Add(argOutput);
rootCmd.Options.Add(argBstn);

rootCmd.SetAction(result =>
{
    var bstnFile = result.GetValue(argBstn);
    JBST? bstn = null;
    if (bstnFile != null)
    {
        using var bstnHandle = bstnFile.OpenRead();
        bstn = JBST.fromStream(new BeBinaryReader(bstnHandle));
    }

    var file = result.GetValue(argFile)!;

    var ms = new MemoryStream();

    {
        using var fs = file.OpenRead();
        fs.CopyTo(ms);
    }

    ms.Position = 0;

    var document = new Document(ms.GetBuffer());

    using var reader = new BinaryReader(ms);

    var magic1 = reader.ReadByte();
    var magic2 = reader.ReadByte();

    if (magic1 != 'S' || magic2 != 'C')
        throw new InvalidOperationException("Invalid magic!");

    var numCategories = reader.ReadUInt16BE();
    var sectionSize = reader.ReadUInt32BE();

    document.Annotations.Insert(new SectionAnnotation(new Interval<uint>(0, 8 + (uint)numCategories * 4), "Header"));

    var entryPoints = new Dictionary<uint, List<(int category, int idx)>>();

    for (var i = 0; i < numCategories; i++)
    {
        reader.BaseStream.Position = 8 + (uint)i * 4;
        var offset = reader.ReadUInt32BE();

        reader.BaseStream.Position = offset;
        var offsetCount = reader.ReadUInt32BE();

        document.Annotations.Insert(
            new SectionAnnotation(new Interval<uint>(offset, 4 + offsetCount * 4), $"Category {i} table"));

        for (var o = 0; o < offsetCount; o++)
        {
            var itemOffset = reader.ReadUInt32BE();
            ref var list = ref CollectionsMarshal.GetValueRefOrAddDefault(entryPoints, itemOffset, out _);
            list ??= [];
            list.Add((i, o));
        }
    }

    using var writer = new StreamWriter(result.GetValue(argOutput)!.Create());

    var disassembler = new Disassembler(reader, document);

    // Start disassembling from entry points defined in headers.
    foreach (var entryPoint in entryPoints)
    {
        try
        {
            disassembler.DisassembleFrom(entryPoint.Key);
        }
        catch (Exception e)
        {
            Console.WriteLine($"Uhhh: {e}");
        }
    }

    ExpandJumpTables(document);
    DisassembleJumpTableTargets(document, reader, disassembler);

    foreach (var annotation in document.Annotations.SearchOverlapping(new Interval<uint>(0, (uint)ms.Length))
                 .OrderBy(c => c.Interval.StartPos))
    {
        switch (annotation)
        {
            case SectionAnnotation section:
                writer.WriteLine(";");
                writer.WriteLine($"; SECTION {section.Name}");
                writer.WriteLine(";");
                break;

            case OpcodeAnnotation opcode:
                var startPos = opcode.Interval.StartPos;
                if (entryPoints.TryGetValue(startPos, out var sounds))
                {
                    foreach (var entry in sounds)
                    {
                        writer.Write($"; ENTRY: {entry.category}, 0x{entry.idx:X04}");

                        if (bstn != null)
                        {
                            var bstnEntry = bstn.categories[0].libraries[entry.category].sounds[entry.idx];
                            writer.Write($" ({bstnEntry.name})");
                        }

                        writer.WriteLine();
                    }
                }

                writer.Write($"{startPos:X6}  ");

                WriteRawBytes(reader, opcode.Interval, writer);

                writer.WriteLine(opcode.Decoded.Text);
                break;

            case JumpTableAnnotation:
                writer.WriteLine("; Jump table");

                for (var i = annotation.Interval.StartPos; (i + 3) <= annotation.Interval.EndPos; i += 3)
                {
                    writer.Write($"{i:X6}  ");
                    WriteRawBytes(reader, new Interval<uint>(i, i + 3), writer);
                    writer.WriteLine();
                }

                break;

            default:
                throw new ArgumentOutOfRangeException();
        }
    }
});

return rootCmd.Parse(args).Invoke();

static void WriteRawBytes(BinaryReader reader, Interval<uint> interval, TextWriter target)
{
    var bytes = "";

    reader.BaseStream.Position = interval.StartPos;

    while (reader.BaseStream.Position < interval.EndPos)
    {
        bytes += $"{reader.ReadByte():X2} ";
    }

    target.Write("{0,-20}", bytes);
}

static void ExpandJumpTables(Document document)
{
    var nodes = document.Annotations.SearchOverlapping(new Interval<uint>(0, (uint)document.Data.Length));

    foreach (var node in nodes.Where(n => n is JumpTableAnnotation))
    {
        var startPos = node.Interval.StartPos;
        uint i;
        for (i = node.Interval.EndPos;; i += 3)
        {
            if (document.Annotations.SearchOverlapping(new Interval<uint>(i, i + 3)).Count > 0)
            {
                break;
            }
        }

        var newNode = new JumpTableAnnotation(new Interval<uint>(startPos, i));
        document.Annotations.Remove(node);
        document.Annotations.Insert(newNode);
    }
}

static void DisassembleJumpTableTargets(Document document, BinaryReader reader, Disassembler disassembler)
{
    var nodes = document.Annotations.SearchOverlapping(new Interval<uint>(0, (uint)document.Data.Length));

    foreach (var node in nodes.Where(n => n is JumpTableAnnotation))
    {
        for (var i = node.Interval.StartPos; (i + 3) <= node.Interval.EndPos; i += 3)
        {
            reader.BaseStream.Position = i;
            var addr = reader.ReadUInt24BE();
            disassembler.DisassembleFrom(addr);
        }
    }
}