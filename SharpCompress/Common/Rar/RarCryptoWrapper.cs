using System;
using System.Collections.Generic;
using System.IO;

namespace SharpCompress.Common.Rar
{
    internal class RarCryptoWrapper : Stream
    {
        private readonly Stream actualStream;
        private readonly byte[] salt;
        private RarRijndael rijndael;
        private readonly Queue<byte> data = new Queue<byte>();

        public RarCryptoWrapper(Stream actualStream, string password, byte[] salt)
        {
            this.actualStream = actualStream;
            this.salt = salt;
            rijndael = RarRijndael.InitializeFrom(password, salt);
        }

        public override void Flush()
        {
            throw new NotSupportedException();
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            throw new NotSupportedException();
        }

        public override void SetLength(long value)
        {
            throw new NotSupportedException();
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            if (salt == null)
            {
                return actualStream.Read(buffer, offset, count);
            }
            return ReadAndDecrypt(buffer, offset, count);
        }

        public int ReadAndDecrypt(byte[] buffer, int offset, int count)
        {
            int queueSize = data.Count;
            int sizeToRead = count - queueSize;

            if (sizeToRead > 0)
            {
                int alignedSize = sizeToRead + ((~sizeToRead + 1) & 0xf);
                for (int i = 0; i < alignedSize / 16; i++)
                {
                    //long ax = System.currentTimeMillis();
                    byte[] cipherText = new byte[RarRijndael.CRYPTO_BLOCK_SIZE];
                    actualStream.Read(cipherText, 0, RarRijndael.CRYPTO_BLOCK_SIZE);

                    var readBytes = rijndael.ProcessBlock(cipherText);
                    foreach (var readByte in readBytes)
                        data.Enqueue(readByte);

                }

                for (int i = 0; i < count; i++)
                    buffer[offset + i] = data.Dequeue();
            }
            return count;
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            throw new NotSupportedException();
        }

        public override bool CanRead
        {
            get { return true; }
        }

        public override bool CanSeek
        {
            get { return false; }
        }

        public override bool CanWrite
        {
            get { return false; }
        }

        public override long Length
        {
            get { throw new NotSupportedException(); }
        }

        public override long Position { get; set; }

        protected override void Dispose(bool disposing)
        {
            if (rijndael != null)
            {
                rijndael.Dispose();
                rijndael = null;
            }
            base.Dispose(disposing);
        }
    }
}