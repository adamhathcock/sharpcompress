namespace SharpCompress.Compressor.LZMA
{
    using SharpCompress.Compressor.LZMA.Utilites;
    using System;
    using System.IO;
    using System.Runtime.InteropServices;
    using System.Security.Cryptography;
    using System.Text;

    internal class AesDecoderStream : DecoderStream2
    {
        private bool isDisposed;
        private byte[] mBuffer;
        private ICryptoTransform mDecoder;
        private int mEnding;
        private long mLimit;
        private int mOffset;
        private Stream mStream;
        private int mUnderflow;
        private long mWritten;

        public AesDecoderStream(Stream input, byte[] info, IPasswordProvider pass, long limit)
        {
            int num;
            byte[] buffer;
            byte[] buffer2;
            this.mStream = input;
            this.mLimit = limit;
            if ((((uint) input.Length) & 15) != 0)
            {
                throw new NotSupportedException("AES decoder does not support padding.");
            }
            this.Init(info, out num, out buffer, out buffer2);
            byte[] bytes = Encoding.Unicode.GetBytes(pass.CryptoGetTextPassword());
            byte[] rgbKey = this.InitKey(num, buffer, bytes);
            using (Rijndael rijndael = Rijndael.Create())
            {
                rijndael.Mode = CipherMode.CBC;
                rijndael.Padding = PaddingMode.None;
                this.mDecoder = rijndael.CreateDecryptor(rgbKey, buffer2);
            }
            this.mBuffer = new byte[0x1000];
        }

        protected override void Dispose(bool disposing)
        {
            try
            {
                if (!this.isDisposed)
                {
                    this.isDisposed = true;
                    if (disposing)
                    {
                        this.mStream.Dispose();
                        this.mDecoder.Dispose();
                    }
                }
            }
            finally
            {
                base.Dispose(disposing);
            }
        }

        private int HandleUnderflow(byte[] buffer, int offset, int count)
        {
            if (this.mUnderflow == 0)
            {
                int inputCount = (this.mEnding - this.mOffset) & -16;
                this.mUnderflow = this.mDecoder.TransformBlock(this.mBuffer, this.mOffset, inputCount, this.mBuffer, this.mOffset);
            }
            if (count > this.mUnderflow)
            {
                count = this.mUnderflow;
            }
            Buffer.BlockCopy(this.mBuffer, this.mOffset, buffer, offset, count);
            this.mWritten += count;
            this.mOffset += count;
            this.mUnderflow -= count;
            return count;
        }

        private void Init(byte[] info, out int numCyclesPower, out byte[] salt, out byte[] iv)
        {
            byte num = info[0];
            numCyclesPower = num & 0x3f;
            if ((num & 0xc0) == 0)
            {
                salt = new byte[0];
                iv = new byte[0];
            }
            else
            {
                int num5;
                int num2 = (num >> 7) & 1;
                int num3 = (num >> 6) & 1;
                if (info.Length == 1)
                {
                    throw new InvalidOperationException();
                }
                byte num4 = info[1];
                num2 += num4 >> 4;
                num3 += num4 & 15;
                if (info.Length < ((2 + num2) + num3))
                {
                    throw new InvalidOperationException();
                }
                salt = new byte[num2];
                for (num5 = 0; num5 < num2; num5++)
                {
                    salt[num5] = info[num5 + 2];
                }
                iv = new byte[0x10];
                for (num5 = 0; num5 < num3; num5++)
                {
                    iv[num5] = info[(num5 + num2) + 2];
                }
                if (numCyclesPower > 0x18)
                {
                    throw new NotSupportedException();
                }
            }
        }

        private byte[] InitKey(int mNumCyclesPower, byte[] salt, byte[] pass)
        {
            int num2;
            if (mNumCyclesPower == 0x3f)
            {
                byte[] buffer = new byte[0x20];
                int index = 0;
                while (index < salt.Length)
                {
                    buffer[index] = salt[index];
                    index++;
                }
                num2 = 0;
                while ((num2 < pass.Length) && (index < 0x20))
                {
                    buffer[index++] = pass[num2];
                    num2++;
                }
                return buffer;
            }
            using (SHA256 sha = SHA256.Create())
            {
                byte[] inputBuffer = new byte[8];
                long num3 = ((long) 1L) << mNumCyclesPower;
                for (long i = 0L; i < num3; i += 1L)
                {
                    sha.TransformBlock(salt, 0, salt.Length, null, 0);
                    sha.TransformBlock(pass, 0, pass.Length, null, 0);
                    sha.TransformBlock(inputBuffer, 0, 8, null, 0);
                    for (num2 = 0; num2 < 8; num2++)
                    {
                        if ((inputBuffer[num2] = (byte) (inputBuffer[num2] + 1)) != 0)
                        {
                            break;
                        }
                    }
                }
                sha.TransformFinalBlock(inputBuffer, 0, 0);
                return sha.Hash;
            }
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            if ((count == 0) || (this.mWritten == this.mLimit))
            {
                return 0;
            }
            if (this.mUnderflow > 0)
            {
                return this.HandleUnderflow(buffer, offset, count);
            }
            if ((this.mEnding - this.mOffset) < 0x10)
            {
                Buffer.BlockCopy(this.mBuffer, this.mOffset, this.mBuffer, 0, this.mEnding - this.mOffset);
                this.mEnding -= this.mOffset;
                this.mOffset = 0;
                do
                {
                    int num = this.mStream.Read(this.mBuffer, this.mEnding, this.mBuffer.Length - this.mEnding);
                    if (num == 0)
                    {
                        throw new EndOfStreamException();
                    }
                    this.mEnding += num;
                }
                while ((this.mEnding - this.mOffset) < 0x10);
            }
            if (count > (this.mLimit - this.mWritten))
            {
                count = (int) (this.mLimit - this.mWritten);
            }
            if (count < 0x10)
            {
                return this.HandleUnderflow(buffer, offset, count);
            }
            if (count > (this.mEnding - this.mOffset))
            {
                count = this.mEnding - this.mOffset;
            }
            int num2 = this.mDecoder.TransformBlock(this.mBuffer, this.mOffset, count & -16, buffer, offset);
            this.mOffset += num2;
            this.mWritten += num2;
            return num2;
        }

        public override long Length
        {
            get
            {
                return this.mLimit;
            }
        }

        public override long Position
        {
            get
            {
                return this.mWritten;
            }
        }
    }
}

