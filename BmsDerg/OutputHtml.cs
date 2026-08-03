using System.Text;

using BmsDerg.Utility;

using xayrga.bast;

namespace BmsDerg;

public sealed class OutputHtml
{
    private const string Header = """
        <!DOCTYPE html>
        <html>
        <head>
            <meta charset="UTF-8" />
            <title>BmsDerg Disassembly</title>
            <style>
            @media (prefers-color-scheme: dark) {
                body {
                    background-color: #1E1E1E;
                    color: #D4D4D4;
                }
                .pos {
                    color: #B5CEA8;
                }
                .comment {
                    color: #6A9955;
                }
                .bytes {
                    color: #CE9178;
                }
                .xref {
                    color: #9CDCFE;
                }
            }
            </style>
        </head>
        <body lang="en-US">
        <pre><code>
        """;

    private const string Footer = """
        </code></pre>
        </body>
        </html>
        """;

    public static void Output(FileInfo file, Document document, BinaryReader reader, JBST? bstn)
    {
        using var writer = new StreamWriter(file.Create());

        writer.Write(Header);

        foreach (var annotation in document.Annotations.GetAll().OrderBy(c => c.Interval.StartPos))
        {
            switch (annotation)
            {
                case SectionAnnotation section:
                    WriteComment(";", writer);
                    WriteComment($"; SECTION {section.Name}", writer);
                    WriteComment(";", writer);
                    break;

                case OpcodeAnnotation opcode:
                    var startPos = opcode.Interval.StartPos;
                    if (document.EntryPoints.TryGetValue(startPos, out var sounds))
                    {
                        foreach (var entry in sounds)
                        {
                            var commentText = $"; ENTRY: {entry.category}, 0x{entry.idx:X04}";

                            if (bstn != null)
                            {
                                var bstnEntry = bstn.categories[0].libraries[entry.category].sounds[entry.idx];
                                commentText += $" ({bstnEntry.name})";
                            }

                            WriteComment(commentText, writer);
                        }
                    }

                    WriteStartPos(startPos, writer);
                    WriteRawBytes(reader, opcode.Interval, writer);

                    var cmdText = DefaultDisassemble(opcode.Decoded.Opcode, opcode.Decoded.Arguments);
                    writer.WriteLine(cmdText);
                    break;

                case JumpTableAnnotation:
                    WriteComment("; Jump table", writer);

                    for (var i = annotation.Interval.StartPos; (i + 3) <= annotation.Interval.EndPos; i += 3)
                    {
                        WriteStartPos(i, writer);
                        WriteRawBytes(reader, new Interval<uint>(i, i + 3), writer);
                        writer.WriteLine();
                    }

                    break;

                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        writer.Write(Footer);
    }

    private static void WriteRawBytes(BinaryReader reader, Interval<uint> interval, TextWriter target)
    {
        var bytes = "";

        reader.BaseStream.Position = interval.StartPos;

        while (reader.BaseStream.Position < interval.EndPos)
        {
            bytes += $"{reader.ReadByte():X2} ";
        }

        target.Write("<span class='bytes'>{0,-40}</span>", bytes);
    }

    private static void WriteStartPos(uint startPos, TextWriter target)
    {
        target.Write($"<span class='pos' id='pos-{startPos:X6}'>{startPos:X6}</span>  ");
    }

    private static void WriteComment(string text, TextWriter target)
    {
        target.Write("<span class='comment'>");
        target.Write(text);
        target.WriteLine("</span>");
    }

    public static string DefaultDisassemble(in Disassembler.OpcodeDef def, Disassembler.OpcodeArgument[] args)
    {
        var sb = new StringBuilder();
        sb.Append(def.Name);
        sb.Append(' ');

        for (var i = 0; i < args.Length; i++)
        {
            if (i != 0)
                sb.Append(',');

            var arg = args[i];
            var param = def.Parameters[i];

            if (param.Format == Disassembler.OpcodeFormat.RegId)
            {
                sb.Append("<span class='register'>");

                var regValue = RegIds.GetRegName(arg.Value);
                if (arg.Type == Disassembler.OpcodeArgumentType.Register)
                {
                    sb.Append($"r({regValue})");
                }
                else
                {
                    sb.Append(regValue);
                }

                sb.Append("</span>");
            }
            else if (arg.Type == Disassembler.OpcodeArgumentType.Register)
            {
                sb.Append($"<span class='register'>{RegIds.GetRegName(arg.Value)}</span>");
            }
            else if (param.Format == Disassembler.OpcodeFormat.Xref)
            {
                sb.Append($"<a class='xref' href='#pos-{arg:X6}'>0x{arg:X06}</a>");
            }
            else
            {
                sb.Append($"<span class='number'>{arg}</span>");
            }
        }

        return sb.ToString();
    }
}
