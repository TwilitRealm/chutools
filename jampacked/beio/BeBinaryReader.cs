using Be.IO.Helpers;
using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace Be.IO
{
    public class BeBinaryReader : BinaryReader
    {
        private static readonly Encoding UTF8NoBom = new UTF8Encoding();

        protected readonly byte[] buffer;

        public BeBinaryReader(Stream s)
            : this(s, UTF8NoBom)
        { }

        public BeBinaryReader(Stream s, Encoding e)
            : this(s, e, false)
        { }

        public BeBinaryReader(Stream s, Encoding e, bool leaveOpen)
            : base(s, e, leaveOpen)
        {
            // Mirror code from BinaryReader.cs
            int bufferSize = e.GetMaxByteCount(1);
            if (bufferSize < 16)
                bufferSize = 16;
            this.buffer = new byte[bufferSize];
        }

        public override decimal ReadDecimal()
        {
            throw new NotSupportedException();
            /*
            FillBuffer(16);
            fixed (byte* p = buffer)
                return BigEndian.ReadDecimal(p);
            */
        }

        public override double ReadDouble()
        {
            FillBuffer(8);
            return BinaryPrimitives.ReadDoubleBigEndian(buffer);
        }

        public override short ReadInt16()
        {
            FillBuffer(2);
            return BinaryPrimitives.ReadInt16BigEndian(buffer);
        }

        public override int ReadInt32()
        {
            FillBuffer(4);
            return BinaryPrimitives.ReadInt32BigEndian(buffer);
        }

        public override long ReadInt64()
        {
            FillBuffer(8);
            return BinaryPrimitives.ReadInt64BigEndian(buffer);
        }

        public override float ReadSingle()
        {
            FillBuffer(4);
            return BinaryPrimitives.ReadSingleBigEndian(buffer);
        }

        public override ushort ReadUInt16()
        {
            FillBuffer(2);
            return BinaryPrimitives.ReadUInt16BigEndian(buffer);
        }

        public override uint ReadUInt32()
        {
            FillBuffer(4);
            return  BinaryPrimitives.ReadUInt32BigEndian(buffer);
        }

        public override ulong ReadUInt64()
        {
            FillBuffer(8);
            return BinaryPrimitives.ReadUInt64BigEndian(buffer);
        }

        protected override void FillBuffer(int numBytes)
        {
            if ((uint)numBytes > buffer.Length)
                Error.Range(nameof(numBytes), "Expected a non-negative value.");
            var s = BaseStream;
            if (s == null)
                Error.Disposed();
            int n, read = 0;
            do
            {
                n = s.Read(buffer, read, numBytes - read);
                if (n == 0)
                    Error.EndOfStream();
                read += n;
            } while (read < numBytes);
        }
    }
}
