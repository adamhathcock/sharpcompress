using System;
using System.Collections.Generic;
using System.IO;

namespace SharpCompress.Common.Rar
{
    internal class RarCryptoWrapper : Stream
    {
        private readonly Stream actualStream;
        private byte[] salt;
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
            throw new NotImplementedException();
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            throw new NotImplementedException();
        }

        public override void SetLength(long value)
        {
            throw new NotImplementedException();
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
                    byte[] cipherText = new byte[RarRijndael.CryptoBlockSize];
                    actualStream.Read(cipherText, 0, RarRijndael.CryptoBlockSize);

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
            throw new NotImplementedException();
        }

        public override bool CanRead
        {
            get { throw new NotImplementedException(); }
        }

        public override bool CanSeek
        {
            get { throw new NotImplementedException(); }
        }

        public override bool CanWrite
        {
            get { throw new NotImplementedException(); }
        }

        public override long Length
        {
            get { throw new NotImplementedException(); }
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