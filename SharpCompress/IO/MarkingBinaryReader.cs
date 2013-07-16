using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using SharpCompress.Common.Rar;

namespace SharpCompress.IO
{
    internal class MarkingBinaryReader : BinaryReader
    {
        private byte[] _salt;
        private readonly string _password;
        private RarRijndael _rijndael;
        private Queue<byte> _data = new Queue<byte>();

        public MarkingBinaryReader(Stream stream, string password = null)
            : base(stream)
        {
            _password = password;
        }

        public long CurrentReadByteCount { get; private set; }

        internal byte[] Salt
        {
            get { return _salt; }
            set
            {
                _salt = value;
                if (value != null) InitializeAes();

            }
        }

        private void InitializeAes()
        {
             _rijndael = RarRijndael.InitializeFrom(_password, _salt);
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
            return ReadBytes(1).Single();
        }

        public override byte[] ReadBytes(int count)
        {
            CurrentReadByteCount += count;
            return UseEncryption ?
                ReadAndDecryptBytes(count)
                : base.ReadBytes(count);
        }

        protected bool UseEncryption
        {
            get { return Salt != null; }
        }

        private byte[] ReadAndDecryptBytes(int count)
        {
            int queueSize = _data.Count;
            int sizeToRead = count - queueSize;

            if (sizeToRead > 0)
            {
                int alignedSize = sizeToRead + ((~sizeToRead + 1) & 0xf);
                for (int i = 0; i < alignedSize / 16; i++)
                {
                    //long ax = System.currentTimeMillis();
                    byte[] cipherText = base.ReadBytes(16);
                    var readBytes = _rijndael.ProcessBlock(cipherText);
                    foreach (var readByte in readBytes)
                        _data.Enqueue(readByte);

                }

            }

            var decryptedBytes = new byte[count];

            for (int i = 0; i < count; i++)
            {
                decryptedBytes[i] = _data.Dequeue();
            }
            return decryptedBytes;
        }

        public override char ReadChar()
        {
            throw new NotImplementedException();
        }

        public override char[] ReadChars(int count)
        {
            throw new NotImplementedException();
        }

#if !PORTABLE
        public override decimal ReadDecimal()
        {
            return ByteArrayToDecimal(ReadBytes(16), 0);
        }

        private decimal ByteArrayToDecimal(byte[] src, int offset)
        {
            //http://stackoverflow.com/a/16984356/385387
            var i1 = BitConverter.ToInt32(src, offset);
            var i2 = BitConverter.ToInt32(src, offset + 4);
            var i3 = BitConverter.ToInt32(src, offset + 8);
            var i4 = BitConverter.ToInt32(src, offset + 12);

            return new decimal(new[] { i1, i2, i3, i4 });
        }
#endif

        public override double ReadDouble()
        {
            return BitConverter.ToDouble(ReadBytes(8), 0);
        }

        public override short ReadInt16()
        {
            return BitConverter.ToInt16(ReadBytes(2), 0);
        }

        public override int ReadInt32()
        {
            return BitConverter.ToInt32(ReadBytes(4), 0);
        }

        public override long ReadInt64()
        {
            return BitConverter.ToInt64(ReadBytes(8), 0);
        }

        public override sbyte ReadSByte()
        {
            return (sbyte)ReadByte();
        }

        public override float ReadSingle()
        {
            return BitConverter.ToSingle(ReadBytes(4), 0);
        }

        public override string ReadString()
        {
            throw new NotImplementedException();
        }

        public override ushort ReadUInt16()
        {
            return BitConverter.ToUInt16(ReadBytes(2), 0);
        }

        public override uint ReadUInt32()
        {
            return BitConverter.ToUInt32(ReadBytes(4), 0);
        }

        public override ulong ReadUInt64()
        {
            return BitConverter.ToUInt64(ReadBytes(8), 0);
        }

        public void ClearQueue()
        {
            _data.Clear();
        }

        public void SkipQueue()
        {
            var position = BaseStream.Position;
            BaseStream.Position = position + _data.Count;
            ClearQueue();
        }
    }
}