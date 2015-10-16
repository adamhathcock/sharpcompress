namespace SharpCompress.Common.SevenZip
{
    using SharpCompress.Compressor.LZMA;
    using System;
    using System.IO;
    using System.Text;

    internal class DataReader
    {
        private byte[] _buffer;
        private int _ending;
        private int _offset;
        private int _origin;

        public DataReader(byte[] buffer, int offset, int length)
        {
            this._buffer = buffer;
            this._origin = offset;
            this._offset = offset;
            this._ending = offset + length;
        }

        public static uint Get32(byte[] buffer, int offset)
        {
            return (uint) (((buffer[offset] + (buffer[offset + 1] << 8)) + (buffer[offset + 2] << 0x10)) + (buffer[offset + 3] << 0x18));
        }

        public static ulong Get64(byte[] buffer, int offset)
        {
            return (ulong) (((((((buffer[offset] + (buffer[offset + 1] << 8)) + (buffer[offset + 2] << 0x10)) + (buffer[offset + 3] << 0x18)) + (buffer[offset + 4] << 0x20)) + (buffer[offset + 5] << 40)) + (buffer[offset + 6] << 0x30)) + (buffer[offset + 7] << 0x38));
        }

        public byte ReadByte()
        {
            if (this._offset >= this._ending)
            {
                throw new EndOfStreamException();
            }
            return this._buffer[this._offset++];
        }

        public void ReadBytes(byte[] buffer, int offset, int length)
        {
            if (length > (this._ending - this._offset))
            {
                throw new EndOfStreamException();
            }
            while (length-- > 0)
            {
                buffer[offset++] = this._buffer[this._offset++];
            }
        }

        public int ReadNum()
        {
            ulong num = this.ReadNumber();
            if (num > 0x7fffffffL)
            {
                throw new NotSupportedException();
            }
            return (int) num;
        }

        public ulong ReadNumber()
        {
            if (this._offset >= this._ending)
            {
                throw new EndOfStreamException();
            }
            byte num = this._buffer[this._offset++];
            byte num2 = 0x80;
            ulong num3 = 0L;
            for (int i = 0; i < 8; i++)
            {
                if ((num & num2) == 0)
                {
                    ulong num5 = (ulong) (num & (num2 - 1));
                    return (num3 + (num5 << (i * 8)));
                }
                if (this._offset >= this._ending)
                {
                    throw new EndOfStreamException();
                }
                num3 |= (ulong)this._buffer[this._offset++] << (8 * i);
                num2 = (byte) (num2 >> 1);
            }
            return num3;
        }

        public string ReadString()
        {
            int index = this._offset;
            while (true)
            {
                if ((index + 2) > this._ending)
                {
                    throw new EndOfStreamException();
                }
                if ((this._buffer[index] == 0) && (this._buffer[index + 1] == 0))
                {
                    string str = Encoding.Unicode.GetString(this._buffer, this._offset, index - this._offset);
                    this._offset = index + 2;
                    return str;
                }
                index += 2;
            }
        }

        public uint ReadUInt32()
        {
            if ((this._offset + 4) > this._ending)
            {
                throw new EndOfStreamException();
            }
            uint num = Get32(this._buffer, this._offset);
            this._offset += 4;
            return num;
        }

        public ulong ReadUInt64()
        {
            if ((this._offset + 8) > this._ending)
            {
                throw new EndOfStreamException();
            }
            ulong num = Get64(this._buffer, this._offset);
            this._offset += 8;
            return num;
        }

        public void SkipData()
        {
            this.SkipData((long) this.ReadNumber());
        }

        public void SkipData(long size)
        {
            if (size > (this._ending - this._offset))
            {
                throw new EndOfStreamException();
            }
            this._offset += (int) size;
            Log.WriteLine("SkipData {0}", new object[] { size });
        }

        public int Offset
        {
            get
            {
                return this._offset;
            }
        }
    }
}

