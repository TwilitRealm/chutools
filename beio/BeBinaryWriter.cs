using Be.IO.Helpers;
using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace Be.IO
{
    public class BeBinaryWriter : BinaryWriter
    {
        private static readonly Encoding UTF8NoBomThrows = new UTF8Encoding(false, true);

        protected readonly byte[] buffer;

        public BeBinaryWriter(Stream s)
            : this(s, UTF8NoBomThrows)
        { }

        public BeBinaryWriter(Stream s, Encoding e)
            : this(s, e, false)
        { }

        public BeBinaryWriter(Stream s, Encoding e, bool leaveOpen)
            : base(s, e, leaveOpen)
        {
            this.buffer = new byte[16];
        }

        public override void Write(decimal value)
        {
            throw new NotSupportedException();
            /*
            fixed (byte* p = buffer)
                BigEndian.WriteDecimal(p, value);
            OutStream.Write(buffer, 0, 16);
            */
        }

        public override void Write(double value)
        {
            BinaryPrimitives.WriteDoubleBigEndian(buffer, value);
            OutStream.Write(buffer, 0, 8);
        }

        public override void Write(float value)
        {
            BinaryPrimitives.WriteSingleBigEndian(buffer, value);
            OutStream.Write(buffer, 0, 4);
        }

        public override void Write(int value)
        {
            BinaryPrimitives.WriteInt32BigEndian(buffer, value);
            OutStream.Write(buffer, 0, 4);
        }

        public override void Write(long value)
        {
            BinaryPrimitives.WriteInt64BigEndian(buffer, value);
            OutStream.Write(buffer, 0, 8);
        }

        public override void Write(short value)
        {
            BinaryPrimitives.WriteInt16BigEndian(buffer, value);
            OutStream.Write(buffer, 0, 2);
        }

        public override void Write(uint value)
        {
            BinaryPrimitives.WriteUInt32BigEndian(buffer, value);
            OutStream.Write(buffer, 0, 4);
        }

        public override void Write(ulong value)
        {
            BinaryPrimitives.WriteUInt64BigEndian(buffer, value);
            OutStream.Write(buffer, 0, 8);
        }

        public override void Write(ushort value)
        {
            BinaryPrimitives.WriteUInt16BigEndian(buffer, value);
            OutStream.Write(buffer, 0, 2);
        }
        
        public void WriteVarInt(int value)
        {
            if (value < 0x80)
            {
                Write((byte)value);
            }
            else if (value < 0x4000)
            {
                Write((byte)(((value >> 7) & 0x7F) | 0x80));
                Write((byte)(value & 0x7F));
            }
            else if (value < 0x20_000)
            {
                Write((byte)(((value >> 14) & 0x7F) | 0x80));
                Write((byte)(((value >> 7) & 0x7F) | 0x80));
                Write((byte)(value & 0x7F));
            }
            else
            {
                Write((byte)(((value >> 21) & 0x7F) | 0x80));
                Write((byte)(((value >> 14) & 0x7F) | 0x80));
                Write((byte)(((value >> 7) & 0x7F) | 0x80));
                Write((byte)(value & 0x7F));
            }
        }
    }
}
