namespace SharpCompress.Common.Zip
{
    using System;
    using System.IO;

    internal class PkwareTraditionalCryptoStream : Stream
    {
        private readonly PkwareTraditionalEncryptionData encryptor;
        private bool isDisposed;
        private readonly CryptoMode mode;
        private readonly Stream stream;

        public PkwareTraditionalCryptoStream(Stream stream, PkwareTraditionalEncryptionData encryptor, CryptoMode mode)
        {
            this.encryptor = encryptor;
            this.stream = stream;
            this.mode = mode;
        }

        protected override void Dispose(bool disposing)
        {
            if (!this.isDisposed)
            {
                this.isDisposed = true;
                base.Dispose(disposing);
                this.stream.Dispose();
            }
        }

        public override void Flush()
        {
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            if (this.mode == CryptoMode.Encrypt)
            {
                throw new NotSupportedException("This stream does not encrypt via Read()");
            }
            if (buffer == null)
            {
                throw new ArgumentNullException("buffer");
            }
            byte[] buffer2 = new byte[count];
            int num = this.stream.Read(buffer2, 0, count);
            Buffer.BlockCopy(this.encryptor.Decrypt(buffer2, num), 0, buffer, offset, num);
            return num;
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            throw new NotSupportedException();
        }

        public override void SetLength(long value)
        {
            throw new NotSupportedException();
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            if (this.mode == CryptoMode.Decrypt)
            {
                throw new NotSupportedException("This stream does not Decrypt via Write()");
            }
            if (count != 0)
            {
                byte[] dst = null;
                if (offset != 0)
                {
                    dst = new byte[count];
                    Buffer.BlockCopy(buffer, offset, dst, 0, count);
                }
                else
                {
                    dst = buffer;
                }
                byte[] buffer3 = this.encryptor.Encrypt(dst, count);
                this.stream.Write(buffer3, 0, buffer3.Length);
            }
        }

        public override bool CanRead
        {
            get
            {
                return (this.mode == CryptoMode.Decrypt);
            }
        }

        public override bool CanSeek
        {
            get
            {
                return false;
            }
        }

        public override bool CanWrite
        {
            get
            {
                return (this.mode == CryptoMode.Encrypt);
            }
        }

        public override long Length
        {
            get
            {
                throw new NotSupportedException();
            }
        }

        public override long Position
        {
            get
            {
                throw new NotSupportedException();
            }
            set
            {
                throw new NotSupportedException();
            }
        }
    }
}

