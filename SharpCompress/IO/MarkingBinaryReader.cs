using System;
using System.IO;

namespace SharpCompress.IO
{
    internal class MarkingBinaryReader : BinaryReader
    {
        public MarkingBinaryReader(Stream stream)
            : base(stream)
        {
        }

        public long CurrentReadByteCount
        {
            get;
            private set;
        }

        public void Mark()
        {
            CurrentReadByteCount = 0;
        }

        public override int Read()
        {
            CurrentReadByteCount += 4;
            return base.Read();
        }

        public override int Read(byte[] buffer, int index, int count)
        {
            CurrentReadByteCount += count;
            return base.Read(buffer, index, count);
        }

        public override int Read(char[] buffer, int index, int count)
        {
            throw new NotImplementedException();
        }

        public override bool ReadBoolean()
        {
            CurrentReadByteCount++;
            return base.ReadBoolean();
        }

        public override byte ReadByte()
        {
            CurrentReadByteCount++;
            return base.ReadByte();
        }

        public override byte[] ReadBytes(int count)
        {
            CurrentReadByteCount += count;
            return base.ReadBytes(count);
        }

        public override char ReadChar()
        {
            throw new NotImplementedException();
        }

        public override char[] ReadChars(int count)
        {

            throw new NotImplementedException();
        }

#if !SILVERLIGHT && !PORTABLE
        public override decimal ReadDecimal()
        {
            CurrentReadByteCount += 16;
            return base.ReadDecimal();
        }
#endif

        public override double ReadDouble()
        {
            CurrentReadByteCount += 8;
            return base.ReadDouble();
        }

        public override short ReadInt16()
        {
            CurrentReadByteCount += 2;
            return base.ReadInt16();
        }

        public override int ReadInt32()
        {
            CurrentReadByteCount += 4;
            return base.ReadInt32();
        }

        public override long ReadInt64()
        {
            CurrentReadByteCount += 8;
            return base.ReadInt64();
        }

        public override sbyte ReadSByte()
        {
            CurrentReadByteCount++;
            return base.ReadSByte();
        }

        public override float ReadSingle()
        {
            CurrentReadByteCount += 4;
            return base.ReadSingle();
        }

        public override string ReadString()
        {
            throw new NotImplementedException();
        }

        public override ushort ReadUInt16()
        {
            CurrentReadByteCount += 2;
            return base.ReadUInt16();
        }

        public override uint ReadUInt32()
        {
            CurrentReadByteCount += 4;
            return base.ReadUInt32();
        }

        public override ulong ReadUInt64()
        {
            CurrentReadByteCount += 8;
            return base.ReadUInt64();
        }
    }
}
