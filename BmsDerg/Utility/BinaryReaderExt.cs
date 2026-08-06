using System.Buffers.Binary;

namespace BmsDerg.Utility;

public static class BinaryReaderExt
{
    extension(BinaryReader br)
    {
        public ushort ReadUInt16BE() => BinaryPrimitives.ReverseEndianness(br.ReadUInt16());
        public uint ReadUInt32BE() => BinaryPrimitives.ReverseEndianness(br.ReadUInt32());
        public uint ReadUInt24BE()
        {
            br.BaseStream.Position -= 1;
            return BinaryPrimitives.ReverseEndianness(br.ReadUInt32()) & 0xFFFFFF;
        }

        public int ReadVarInt32()
        {
            int byt = br.ReadByte();

            if ((byt & 0x80) == 0)
                return byt;

            byt &= 0x7f;

            int i = 0;

            while (true)
            {
                if (2 < i)
                    throw new InvalidOperationException("Too large value");

                byte newByte = br.ReadByte();

                byt = byt << 7;
                byt |= newByte & 0x7f;

                if ((newByte & 0x80) == 0)
                    break;

                i++;
            }

            return byt;
        }
    }
}