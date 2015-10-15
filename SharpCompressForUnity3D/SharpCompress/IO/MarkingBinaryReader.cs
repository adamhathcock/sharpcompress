namespace SharpCompress.IO
{
    using System;
    using System.IO;
    using System.Linq;
    using System.Runtime.CompilerServices;

    internal class MarkingBinaryReader : BinaryReader
    {
        [CompilerGenerated]
        private long <CurrentReadByteCount>k__BackingField;

        public MarkingBinaryReader(Stream stream) : base(stream)
        {
        }

        private decimal ByteArrayToDecimal(byte[] src, int offset)
        {
            int num = BitConverter.ToInt32(src, offset);
            int num2 = BitConverter.ToInt32(src, offset + 4);
            int num3 = BitConverter.ToInt32(src, offset + 8);
            int num4 = BitConverter.ToInt32(src, offset + 12);
            return new decimal(new int[] { num, num2, num3, num4 });
        }

        public void Mark()
        {
            this.CurrentReadByteCount = 0L;
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
            return BitConverter.ToBoolean(this.ReadBytes(1), 0);
        }

        public override byte ReadByte()
        {
            return Enumerable.Single<byte>(this.ReadBytes(1));
        }

        public override byte[] ReadBytes(int count)
        {
            this.CurrentReadByteCount += count;
            byte[] buffer = base.ReadBytes(count);
            if (buffer.Length != count)
            {
                throw new EndOfStreamException(string.Format("Could not read the requested amount of bytes.  End of stream reached. Requested: {0} Read: {1}", count, buffer.Length));
            }
            return buffer;
        }

        public override char ReadChar()
        {
            throw new NotSupportedException();
        }

        public override char[] ReadChars(int count)
        {
            throw new NotSupportedException();
        }

        public override decimal ReadDecimal()
        {
            return this.ByteArrayToDecimal(this.ReadBytes(0x10), 0);
        }

        public override double ReadDouble()
        {
            return BitConverter.ToDouble(this.ReadBytes(8), 0);
        }

        public override short ReadInt16()
        {
            return BitConverter.ToInt16(this.ReadBytes(2), 0);
        }

        public override int ReadInt32()
        {
            return BitConverter.ToInt32(this.ReadBytes(4), 0);
        }

        public override long ReadInt64()
        {
            return BitConverter.ToInt64(this.ReadBytes(8), 0);
        }

        public override sbyte ReadSByte()
        {
            return (sbyte) this.ReadByte();
        }

        public override float ReadSingle()
        {
            return BitConverter.ToSingle(this.ReadBytes(4), 0);
        }

        public override string ReadString()
        {
            throw new NotSupportedException();
        }

        public override ushort ReadUInt16()
        {
            return BitConverter.ToUInt16(this.ReadBytes(2), 0);
        }

        public override uint ReadUInt32()
        {
            return BitConverter.ToUInt32(this.ReadBytes(4), 0);
        }

        public override ulong ReadUInt64()
        {
            return BitConverter.ToUInt64(this.ReadBytes(8), 0);
        }

        public long CurrentReadByteCount
        {
            [CompilerGenerated]
            get
            {
                return this.<CurrentReadByteCount>k__BackingField;
            }
            [CompilerGenerated]
            private set
            {
                this.<CurrentReadByteCount>k__BackingField = value;
            }
        }
    }
}

