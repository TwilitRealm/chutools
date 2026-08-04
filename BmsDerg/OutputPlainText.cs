using System.Text;

using BmsDerg.Utility;

using xayrga.bast;

namespace BmsDerg;

public sealed class OutputPlainText
{
    public static void Output(FileInfo file, Document document, BinaryReader reader, JBST? bstn)
    {
        using var writer = new StreamWriter(file.Create());

        foreach (var annotation in document.Annotations.GetAll().OrderBy(c => c.Interval.StartPos))
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
                    if (document.Xrefs.TryGetValue(startPos, out var sounds))
                    {
                        foreach (var entry in sounds)
                        {
                            writer.Write($"; XREF: {entry.Type} {entry.Category}, 0x{entry.Idx:X04}");

                            if (entry.Type == XrefType.EntryPoint && bstn != null)
                            {
                                var bstnEntry = bstn.categories[0].libraries[entry.Category].sounds[entry.Idx];
                                writer.Write($" ({bstnEntry.name})");
                            }

                            writer.WriteLine();
                        }
                    }

                    writer.Write($"{startPos:X6}  ");

                    WriteRawBytes(reader, opcode.Interval, writer);

                    var decodedText = DefaultDisassemble(opcode.Decoded.Opcode, opcode.Decoded.Arguments);
                    writer.WriteLine(decodedText);
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

    }

    private static void WriteRawBytes(BinaryReader reader, Interval<uint> interval, TextWriter target)
    {
        var bytes = "";

        reader.BaseStream.Position = interval.StartPos;

        while (reader.BaseStream.Position < interval.EndPos)
        {
            bytes += $"{reader.ReadByte():X2} ";
        }

        target.Write("{0,-20}", bytes);
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

            string? fmt = null;
            if (param.Format == Disassembler.OpcodeFormat.RegId)
            {
                sb.Append('r');
            }
            else if (param.Format == Disassembler.OpcodeFormat.Xref)
            {
                fmt = "X06";
                sb.Append("0x");
            }

            sb.Append(arg.ToString(fmt, null));
        }

        return sb.ToString();
    }
}