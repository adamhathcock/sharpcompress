using System;
using System.IO;
using System.Linq;
using SharpCompress.Converter;

namespace SharpCompress.IO
{
    internal class MarkingBinaryReader : BinaryReader
    {

        public MarkingBinaryReader(Stream stream)
            : base(stream)
        {
        }

        public long CurrentReadByteCount { get; private set; }

        public void Mark()
        {
            CurrentReadByteCount = 0;
        }

        public override int Read()
        {
            throw new NotSupportedException();
        }

        public override int Read(byte[] buffer, int index, int count)
        {
            throw new NotSupportedException();
        }

        public override int Read(char[] buffer, int index, int count)
        {
            throw new NotSupportedException();
        }

        public override bool ReadBoolean()
        {
            return ReadBytes(1).Single() != 0;
        }

        public override byte ReadByte()
        {
            return ReadBytes(1).Single();
        }

        public override byte[] ReadBytes(int count)
        {
            CurrentReadByteCount += count;
            var bytes = base.ReadBytes(count);
            if (bytes.Length != count)
            {
                throw new EndOfStreamException(string.Format("Could not read the requested amount of bytes.  End of stream reached. Requested: {0} Read: {1}", count, bytes.Length));
            }
            return bytes;
        }

        public override char ReadChar()
        {
            throw new NotSupportedException();
        }

        public override char[] ReadChars(int count)
        {
            throw new NotSupportedException();
        }

#if !SILVERLIGHT
        public override decimal ReadDecimal()
        {
            throw new NotSupportedException();
        }
#endif

        public override double ReadDouble()
        {
            throw new NotSupportedException();
        }

        public override short ReadInt16()
        {
            return DataConverter.LittleEndian.GetInt16(ReadBytes(2), 0);
        }

        public override int ReadInt32()
        {
            return DataConverter.LittleEndian.GetInt32(ReadBytes(4), 0);
        }

        public override long ReadInt64()
        {
            return DataConverter.LittleEndian.GetInt64(ReadBytes(8), 0);
        }

        public override sbyte ReadSByte()
        {
            return (sbyte)ReadByte();
        }

        public override float ReadSingle()
        {
            throw new NotSupportedException();
        }

        public override string ReadString()
        {
            throw new NotSupportedException();
        }

        public override ushort ReadUInt16()
        {
            return DataConverter.LittleEndian.GetUInt16(ReadBytes(2), 0);
        }

        public override uint ReadUInt32()
        {
            return DataConverter.LittleEndian.GetUInt32(ReadBytes(4), 0);
        }

        public override ulong ReadUInt64()
        {
            return DataConverter.LittleEndian.GetUInt64(ReadBytes(8), 0);
        }
    }
}