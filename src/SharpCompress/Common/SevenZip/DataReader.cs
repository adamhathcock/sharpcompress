using System;
using System.IO;
using System.Text;
using SharpCompress.Compressor.LZMA;

namespace SharpCompress.Common.SevenZip
{
    internal class DataReader
    {
        #region Static Methods

        public static uint Get32(byte[] buffer, int offset)
        {
            return (uint) buffer[offset]
                   + ((uint) buffer[offset + 1] << 8)
                   + ((uint) buffer[offset + 2] << 16)
                   + ((uint) buffer[offset + 3] << 24);
        }

        public static ulong Get64(byte[] buffer, int offset)
        {
            return (ulong) buffer[offset]
                   + ((ulong) buffer[offset + 1] << 8)
                   + ((ulong) buffer[offset + 2] << 16)
                   + ((ulong) buffer[offset + 3] << 24)
                   + ((ulong) buffer[offset + 4] << 32)
                   + ((ulong) buffer[offset + 5] << 40)
                   + ((ulong) buffer[offset + 6] << 48)
                   + ((ulong) buffer[offset + 7] << 56);
        }

        #endregion

        #region Variables

        private byte[] _buffer;
        private int _offset;
        private int _ending;

        #endregion

        #region Public Methods

        public DataReader(byte[] buffer, int offset, int length)
        {
            _buffer = buffer;
            _offset = offset;
            _ending = offset + length;
        }

        public int Offset
        {
            get { return _offset; }
        }

        public Byte ReadByte()
        {
            if (_offset >= _ending)
                throw new EndOfStreamException();

            return _buffer[_offset++];
        }

        public void ReadBytes(byte[] buffer, int offset, int length)
        {
            if (length > _ending - _offset)
                throw new EndOfStreamException();

            while (length-- > 0)
                buffer[offset++] = _buffer[_offset++];
        }

        public void SkipData(long size)
        {
            if (size > _ending - _offset)
                throw new EndOfStreamException();

            _offset += (int) size;
#if DEBUG
            Log.WriteLine("SkipData {0}", size);
#endif
        }

        public void SkipData()
        {
            SkipData(checked((long) ReadNumber()));
        }

        public ulong ReadNumber()
        {
            if (_offset >= _ending)
                throw new EndOfStreamException();

            byte firstByte = _buffer[_offset++];
            byte mask = 0x80;
            ulong value = 0;

            for (int i = 0; i < 8; i++)
            {
                if ((firstByte & mask) == 0)
                {
                    ulong highPart = firstByte & (mask - 1u);
                    value += highPart << (i*8);
                    return value;
                }

                if (_offset >= _ending)
                    throw new EndOfStreamException();

                value |= (ulong) _buffer[_offset++] << (8*i);
                mask >>= 1;
            }

            return value;
        }

        public int ReadNum()
        {
            ulong value = ReadNumber();
            if (value > Int32.MaxValue)
                throw new NotSupportedException();

            return (int) value;
        }

        public uint ReadUInt32()
        {
            if (_offset + 4 > _ending)
                throw new EndOfStreamException();

            uint res = Get32(_buffer, _offset);
            _offset += 4;
            return res;
        }

        public ulong ReadUInt64()
        {
            if (_offset + 8 > _ending)
                throw new EndOfStreamException();

            ulong res = Get64(_buffer, _offset);
            _offset += 8;
            return res;
        }

        public string ReadString()
        {
            int ending = _offset;

            for (;;)
            {
                if (ending + 2 > _ending)
                    throw new EndOfStreamException();

                if (_buffer[ending] == 0 && _buffer[ending + 1] == 0)
                    break;

                ending += 2;
            }

            string str = Encoding.Unicode.GetString(_buffer, _offset, ending - _offset);
            _offset = ending + 2;
            return str;
        }

        #endregion
    }
}