using System;
using System.IO;
using System.Linq;
using SharpCompress.Converters;

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
            int read = base.Read(buffer, index, count);
            CurrentReadByteCount += read;
            return read;
        }

        public override int Read(char[] buffer, int index, int count)
        {
            throw new NotSupportedException();
        }

        public override bool ReadBoolean()
        {
            using (var b = this.ReadScope(1))
            {
                return b.Array.First() != 0;
            }
        }

        public override byte ReadByte()
        {
            using (var b = this.ReadScope(1))
            {
                return b.Array.First();
            }
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
            using (var b = this.ReadScope(2))
            {
                return DataConverter.LittleEndian.GetInt16(b.Array, 0);
            }
        }

        public override int ReadInt32()
        {
            using (var b = this.ReadScope(4))
            {
                return DataConverter.LittleEndian.GetInt32(b.Array, 0);
            }
        }

        public override long ReadInt64()
        {
            using (var b = this.ReadScope(8))
            {
                return DataConverter.LittleEndian.GetInt64(b.Array, 0);
            }
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
            using (var b = this.ReadScope(2))
            {
                return DataConverter.LittleEndian.GetUInt16(b.Array, 0);
            }
        }

        public override uint ReadUInt32()
        {
            using (var b = this.ReadScope(4))
            {
                return DataConverter.LittleEndian.GetUInt32(b.Array, 0);
            }
        }

        public override ulong ReadUInt64()
        {
            using (var b = this.ReadScope(8))
            {
                return DataConverter.LittleEndian.GetUInt64(b.Array, 0);
            }
        }
    }
}