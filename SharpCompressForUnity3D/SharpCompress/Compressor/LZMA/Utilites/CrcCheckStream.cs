namespace SharpCompress.Compressor.LZMA.Utilites
{
    using SharpCompress.Compressor.LZMA;
    using System;
    using System.Diagnostics;
    using System.IO;

    internal class CrcCheckStream : Stream
    {
        private long[] mBytes = new long[0x100];
        private bool mClosed;
        private uint mCurrentCRC;
        private readonly uint mExpectedCRC;
        private long mLength;

        public CrcCheckStream(uint crc)
        {
            this.mExpectedCRC = crc;
            this.mCurrentCRC = uint.MaxValue;
        }

        protected override void Dispose(bool disposing)
        {
            if (this.mCurrentCRC != this.mExpectedCRC)
            {
                throw new InvalidOperationException();
            }
            try
            {
                if (disposing && !this.mClosed)
                {
                    this.mClosed = true;
                    this.mCurrentCRC = CRC.Finish(this.mCurrentCRC);
                    if (this.mCurrentCRC == this.mExpectedCRC)
                    {
                        Debug.WriteLine("CRC ok: " + this.mExpectedCRC.ToString("x8"));
                    }
                    else
                    {
                        Debugger.Break();
                        Debug.WriteLine("bad CRC");
                    }
                    double num = 1.0 / ((double) this.mLength);
                    double num2 = 0.0;
                    for (int i = 0; i < 0x100; i++)
                    {
                        if (this.mBytes[i] != 0L)
                        {
                            double a = num * this.mBytes[i];
                            num2 -= a * Math.Log(a, 256.0);
                        }
                    }
                    Debug.WriteLine("entropy: " + ((int) (num2 * 100.0)) + "%");
                }
            }
            finally
            {
                base.Dispose(disposing);
            }
        }

        public override void Flush()
        {
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            throw new InvalidOperationException();
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
            this.mLength += count;
            for (int i = 0; i < count; i++)
            {
                this.mBytes[buffer[offset + i]] += 1L;
            }
            this.mCurrentCRC = CRC.Update(this.mCurrentCRC, buffer, offset, count);
        }

        public override bool CanRead
        {
            get
            {
                return false;
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
                return true;
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

