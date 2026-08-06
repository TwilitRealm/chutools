using System.Net;
using System.Text;
using System.Text.RegularExpressions;

using BmsDerg.Utility;

using xayrga.bast;

namespace BmsDerg;

public sealed partial class OutputHtml
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
                .pos, .number {
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
                .opcode {
                    color: #DCDCAA;
                }
                .register {
                    color: #569CD6;
                }
                .terminating {
                    border-bottom: solid 1px #D4D4D4;
                    padding-bottom: 1px;
                    margin-bottom: 8px;
                    display: inline-block;
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

                    BeforeAddress(document, startPos, writer, bstn);

                    if (opcode.Status != Disassembler.DisassembleResultStatus.Continue)
                        writer.Write("<span class='terminating'>");

                    WriteStartPos(startPos, writer);
                    WriteRawBytes(reader, opcode.Interval, writer);

                    var cmdText = DefaultDisassemble(opcode.Decoded.Opcode, opcode.Decoded.Arguments);
                    writer.Write(cmdText);

                    if (opcode.Status != Disassembler.DisassembleResultStatus.Continue)
                        writer.Write("</span>");

                    var opcodeLength = PlainTextLength(cmdText);

                    if (opcode.Decoded.Explanation is { } explanation)
                    {
                        const int pad = 40;
                        if (opcodeLength < pad)
                            writer.Write(new string(' ', pad - opcodeLength));

                        WriteComment(" ; " + WebUtility.HtmlEncode(explanation), writer);
                    }
                    else
                    {
                        writer.WriteLine();
                    }
                    break;

                case JumpTableAnnotation:
                    {
                        WriteComment("; Jump table", writer);
                        BeforeAddress(document, annotation.Interval.StartPos, writer, bstn);

                        var c = 0;
                        for (var i = annotation.Interval.StartPos; (i + 3) <= annotation.Interval.EndPos; i += 3)
                        {
                            WriteStartPos(i, writer);
                            WriteRawBytes(reader, new Interval<uint>(i, i + 3), writer);
                            reader.BaseStream.Position = i;
                            writer.Write(XrefLink(reader.ReadUInt24BE()));

                            WriteComment($" ; {c}", writer);

                            c += 1;
                        }

                        break;
                    }

                case RegTableAnnotation regTbl:
                    {
                        WriteComment($"; Reg table ({regTbl.DataType})", writer);
                        BeforeAddress(document, annotation.Interval.StartPos, writer, bstn);

                        var c = 0;
                        for (var i = annotation.Interval.StartPos;
                             (i + regTbl.Step) <= annotation.Interval.EndPos;
                             i += regTbl.Step)
                        {
                            WriteStartPos(i, writer);
                            WriteRawBytes(reader, new Interval<uint>(i, i + regTbl.Step), writer);
                            reader.BaseStream.Position = i;

                            WriteComment($" ; {c}", writer);

                            c += 1;
                        }

                        break;
                    }

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
        sb.Append($"<span class='opcode'>{def.Name.PadRight(10)}</span>");
        sb.Append(' ');

        for (var i = 0; i < args.Length; i++)
        {
            if (i != 0)
                sb.Append(", ");

            var arg = args[i];
            var param = def.Parameters[i];

            if (param.Name != null)
                sb.Append($"<span title='{param.Name}'>");

            if (param.Format == Disassembler.OpcodeFormat.RegId)
            {
                sb.Append("<span class='register'>");

                sb.Append(RegIds.RegIdArg(arg));

                sb.Append("</span>");
            }
            else if (arg.Type == Disassembler.OpcodeArgumentType.Register)
            {
                sb.Append($"<span class='register'>{RegIds.GetRegName(arg.Value)}</span>");
            }
            else if (param.Format == Disassembler.OpcodeFormat.Xref)
            {
                sb.Append(XrefLink(arg.Value));
            }
            else
            {
                sb.Append($"<span class='number'>{arg}</span>");
            }

            if (param.Name != null)
                sb.Append($"</span>");
        }

        return sb.ToString();
    }

    private static string XrefLink(uint value)
    {
        return $"<a class='xref' href='#pos-{value:X6}'>0x{value:X06}</a>";
    }

    private static void BeforeAddress(Document document, uint address, TextWriter writer, JBST? bstn)
    {
        if (!document.Xrefs.TryGetValue(address, out var sounds))
            return;

        foreach (var entry in sounds.OrderBy(e => e.Type).ThenBy(e => e.Category).ThenBy(e => e.Idx))
        {
            if (entry.Type == XrefType.EntryPoint)
            {
                var commentText = $"; ENTRY: {entry.Category}, 0x{entry.Idx:X04}";

                if (bstn != null)
                {
                    var bstnEntry = bstn.categories[0].libraries[entry.Category].sounds[entry.Idx];
                    commentText += $" ({bstnEntry.name})";
                }

                WriteComment(commentText, writer);
            }
            else
            {
                var commentText = $"; XREF: {XrefLink(entry.Idx)}";
                WriteComment(commentText, writer);
            }
        }
    }

    private static int PlainTextLength(string html)
    {
        return HtmlMatcherRegex().Replace(html, "").Length;
    }

    [GeneratedRegex("<.+?>")]
    private static partial Regex HtmlMatcherRegex();
}
