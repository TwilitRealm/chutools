using System.CommandLine;

using Be.IO;

using BmsDerg;
using BmsDerg.Utility;

using xayrga.bast;

var argFile = new Argument<FileInfo>("input");
var argOutput = new Argument<FileInfo>("output");
var argBstn = new Option<FileInfo>("--bstn");
var optionFormat = new Option<DisassemblyFormat>("--format");

var rootCmd = new RootCommand();

var bscCmd = new Command("bsc");
bscCmd.Arguments.Add(argFile);
bscCmd.Arguments.Add(argOutput);
bscCmd.Options.Add(argBstn);
bscCmd.Options.Add(optionFormat);

var bmsCmd = new Command("bms");
bmsCmd.Arguments.Add(argFile);
bmsCmd.Arguments.Add(argOutput);
bmsCmd.Options.Add(optionFormat);

rootCmd.Subcommands.Add(bscCmd);
rootCmd.Subcommands.Add(bmsCmd);

bscCmd.SetAction(result =>
{
    var bstnFile = result.GetValue(argBstn);
    JBST? bstn = null;
    if (bstnFile != null)
    {
        using var bstnHandle = bstnFile.OpenRead();
        bstn = JBST.fromStream(new BeBinaryReader(bstnHandle));
    }

    var ms = LoadInput(result);
    var document = new Document(ms.GetBuffer());
    using var reader = new BinaryReader(ms);

    var magic1 = reader.ReadByte();
    var magic2 = reader.ReadByte();

    if (magic1 != 'S' || magic2 != 'C')
        throw new InvalidOperationException("Invalid magic!");

    var numCategories = reader.ReadUInt16BE();
    var sectionSize = reader.ReadUInt32BE();

    document.Annotations.Insert(new SectionAnnotation(new Interval<uint>(0, 8 + (uint)numCategories * 4), "Header"));

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
            document.InsertXref(itemOffset, Xref.FromEntry(i, o));
        }
    }

    var disassembler = new Disassembler(reader, document);

    // Start disassembling from entry points defined in headers.
    var entryPoints = document.Xrefs.Where(kv => kv.Value.Any(x => x.Type == XrefType.EntryPoint)).ToArray();
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

    FurtherDisassemble(document, reader, disassembler);
    DoOutput(result, document, reader, bstn);
});

bmsCmd.SetAction(result =>
{
    var ms = LoadInput(result);
    var document = new Document(ms.GetBuffer());
    using var reader = new BinaryReader(ms);

    var disassembler = new Disassembler(reader, document);
    disassembler.DisassembleFrom(0);

    FurtherDisassemble(document, reader, disassembler);
    DoOutput(result, document, reader, null);
});

return rootCmd.Parse(args).Invoke();

void DoOutput(ParseResult result, Document document, BinaryReader reader, JBST? bstn)
{
    switch (result.GetValue(optionFormat))
    {
        case DisassemblyFormat.PlainText:
            OutputPlainText.Output(result.GetValue(argOutput)!, document, reader, bstn);
            break;
        case DisassemblyFormat.Html:
            OutputHtml.Output(result.GetValue(argOutput)!, document, reader, bstn);
            break;
        default:
            throw new ArgumentOutOfRangeException();
    }
}

static void FurtherDisassemble(Document document, BinaryReader reader, Disassembler disassembler)
{
    ExpandTables(document);
    DisassembleJumpTableTargets(document, reader, disassembler);
}

static void ExpandSingleTable(Document document, ITableAnnotation table)
{
    var startPos = table.Interval.StartPos;
    uint i;
    for (i = table.Interval.EndPos;; i += table.Step)
    {
        if (document.Annotations.SearchOverlapping(new Interval<uint>(i, i + table.Step)).Count > 0)
        {
            break;
        }
    }

    var newNode = table.Expanded(new Interval<uint>(startPos, i));
    document.Annotations.Remove((BaseDocumentAnnotation) table);
    document.Annotations.Insert(newNode);
}

static void ExpandTables(Document document)
{
    var nodes = document.Annotations.SearchOverlapping(new Interval<uint>(0, (uint)document.Data.Length));

    foreach (var node in nodes.OfType<ITableAnnotation>())
    {
        ExpandSingleTable(document, node);
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
            document.InsertXref(addr, Xref.FromXref(i));

            try
            {
                disassembler.DisassembleFrom(addr);
            }
            catch (Exception e)
            {
                Console.WriteLine($"Uhhh: {e}");
            }
        }
    }
}

MemoryStream LoadInput(ParseResult result)
{
    var file = result.GetValue(argFile)!;
    var ms = new MemoryStream();

    {
        using var fs = file.OpenRead();
        fs.CopyTo(ms);
    }

    ms.Position = 0;
    return ms;
}

public enum DisassemblyFormat
{
    PlainText,
    Html
}