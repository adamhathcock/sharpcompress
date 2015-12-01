namespace SharpCompress.Common.Zip
{
    using System;
    using System.IO;
    using System.Security.Cryptography;

    internal class WinzipAesCryptoStream : Stream
    {
        private const int BLOCK_SIZE_IN_BYTES = 0x10;
        private readonly SymmetricAlgorithm cipher;
        private readonly byte[] counter = new byte[0x10];
        private byte[] counterOut = new byte[0x10];
        private bool isDisposed;
        private bool isFinalBlock;
        private int nonce = 1;
        private readonly Stream stream;
        private long totalBytesLeftToRead;
        private readonly ICryptoTransform transform;

        internal WinzipAesCryptoStream(Stream stream, WinzipAesEncryptionData winzipAesEncryptionData, long length)
        {
            this.stream = stream;
            this.totalBytesLeftToRead = length;
            this.cipher = this.CreateCipher(winzipAesEncryptionData);
            byte[] rgbIV = new byte[0x10];
            this.transform = this.cipher.CreateEncryptor(winzipAesEncryptionData.KeyBytes, rgbIV);
        }

        private SymmetricAlgorithm CreateCipher(WinzipAesEncryptionData winzipAesEncryptionData)
        {
            RijndaelManaged managed = new RijndaelManaged();
            managed.BlockSize = 0x80;
            managed.KeySize = winzipAesEncryptionData.KeyBytes.Length * 8;
            managed.Mode = CipherMode.ECB;
            managed.Padding = PaddingMode.None;
            return managed;
        }

        protected override void Dispose(bool disposing)
        {
            if (!this.isDisposed)
            {
                this.isDisposed = true;
                if (disposing)
                {
                    byte[] buffer = new byte[10];
                    this.stream.Read(buffer, 0, 10);
                    this.stream.Dispose();
                }
            }
        }

        public override void Flush()
        {
            throw new NotSupportedException();
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            if (this.totalBytesLeftToRead == 0L)
            {
                return 0;
            }
            int totalBytesLeftToRead = count;
            if (count > this.totalBytesLeftToRead)
            {
                totalBytesLeftToRead = (int) this.totalBytesLeftToRead;
            }
            int num2 = this.stream.Read(buffer, offset, totalBytesLeftToRead);
            this.totalBytesLeftToRead -= num2;
            this.ReadTransformBlocks(buffer, offset, num2);
            return num2;
        }

        private void ReadTransformBlocks(byte[] buffer, int offset, int count)
        {
            int num = offset;
            int last = count + offset;
            while ((num < buffer.Length) && (num < last))
            {
                int num3 = this.ReadTransformOneBlock(buffer, num, last);
                num += num3;
            }
        }

        private int ReadTransformOneBlock(byte[] buffer, int offset, int last)
        {
            if (this.isFinalBlock)
            {
                throw new InvalidOperationException();
            }
            int num = last - offset;
            int count = (num > 0x10) ? 0x10 : num;
            Array.Copy(BitConverter.GetBytes(this.nonce++), 0, this.counter, 0, 4);
            if ((count == num) && (this.totalBytesLeftToRead == 0L))
            {
                this.counterOut = this.transform.TransformFinalBlock(this.counter, 0, 0x10);
                this.isFinalBlock = true;
            }
            else
            {
                this.transform.TransformBlock(this.counter, 0, 0x10, this.counterOut, 0);
            }
            this.XorInPlace(buffer, offset, count);
            return count;
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
            throw new NotSupportedException();
        }

        private void XorInPlace(byte[] buffer, int offset, int count)
        {
            for (int i = 0; i < count; i++)
            {
                buffer[offset + i] = (byte) (this.counterOut[i] ^ buffer[offset + i]);
            }
        }

        public override bool CanRead
        {
            get
            {
                return true;
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
                return false;
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

