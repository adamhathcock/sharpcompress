
#if !NO_CRYPTO
using System.Collections.Generic;
using System.IO;
using SharpCompress.IO;

namespace SharpCompress.Common.Rar
{
    internal class RarCryptoBinaryReader : RarCrcBinaryReader
    {
        private RarRijndael rijndael;
        private byte[] salt;
        private readonly string password;
        private readonly Queue<byte> data = new Queue<byte>();
        private long readCount;

        public RarCryptoBinaryReader(Stream stream, string password )
            : base(stream)
        {
            this.password = password;
        }

        // track read count ourselves rather than using the underlying stream since we buffer
        public override long CurrentReadByteCount {
            get 
            {
                return this.readCount;
            }
            protected set 
            {
                // ignore
            }
        }

        public override void Mark() {
            this.readCount = 0;
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
            if (UseEncryption)
            {
                return ReadAndDecryptBytes(count);
            }
            this.readCount += count;
            return base.ReadBytes(count);
        }

        private byte[] ReadAndDecryptBytes(int count)
        {
            int queueSize = data.Count;
            int sizeToRead = count - queueSize;

            if (sizeToRead > 0)
            {
                int alignedSize = sizeToRead + ((~sizeToRead + 1) & 0xf);
                for (int i = 0; i < alignedSize / 16; i++)
                {
                    //long ax = System.currentTimeMillis();
                    byte[] cipherText = base.ReadBytesNoCrc(16);
                    var readBytes = rijndael.ProcessBlock(cipherText);
                    foreach (var readByte in readBytes)
                        data.Enqueue(readByte);

                }

            }

            var decryptedBytes = new byte[count];

            for (int i = 0; i < count; i++)
            {
                var b = data.Dequeue();
                decryptedBytes[i] = b;
                UpdateCrc(b);
            }
            this.readCount += count;
            return decryptedBytes;
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