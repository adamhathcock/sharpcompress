
#if !NO_CRYPTO
using System;
using System.Collections.Generic;
using System.IO;
using SharpCompress.IO;

namespace SharpCompress.Common.Rar
{
    internal class RarCryptoBinaryReader : MarkingBinaryReader
    {
        private RarRijndael rijndael;
        private byte[] salt;
        private readonly string password;
        private readonly Queue<byte> data = new Queue<byte>();

        public RarCryptoBinaryReader(Stream stream, string password )
            : base(stream)
        {
            this.password = password;
        }

        protected bool UseEncryption
        {
            get { return salt != null; }
        }

        internal void InitializeAes(byte[] salt)
        {
            this.salt = salt;
            rijndael = RarRijndael.InitializeFrom(password, salt);
        }

        public override byte[] ReadBytes(int count)
        {
            byte[] b = new byte[count];
            Read(b, 0, count);
            return b;
        }

        public override int Read(byte[] buffer, int index, int count)
        {
            if (UseEncryption)
            {
                return ReadAndDecryptBytes(buffer, index, count);
            }
            return base.Read(buffer, index, count);
        }

        private int ReadAndDecryptBytes(byte[] buffer, int index, int count)
        {
            int queueSize = data.Count;
            int sizeToRead = count - queueSize;

            if (sizeToRead > 0)
            {
                int alignedSize = sizeToRead + ((~sizeToRead + 1) & 0xf);
                for (int i = 0; i < alignedSize / 16; i++)
                {
                    //long ax = System.currentTimeMillis();
                    byte[] cipherText = base.ReadBytes(16);
                    var readBytes = rijndael.ProcessBlock(cipherText);
                    foreach (var readByte in readBytes)
                    {
                        data.Enqueue(readByte);
                    }
                }
            }
            
            for (int i = index; i < count; i++)
            {
                buffer[i] = data.Dequeue();
            }
            return count;
        }

        public void ClearQueue()
        {
            data.Clear();
        }

        public void SkipQueue()
        {
            var position = BaseStream.Position;
            BaseStream.Position = position + data.Count;
            ClearQueue();
        }
    }
}
#endif