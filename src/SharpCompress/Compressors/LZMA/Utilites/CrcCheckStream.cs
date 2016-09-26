using System;
using System.Diagnostics;
using System.IO;

namespace SharpCompress.Compressors.LZMA.Utilites
{
    internal class CrcCheckStream : Stream
    {
        private readonly uint mExpectedCRC;
        private uint mCurrentCRC;
        private bool mClosed;

        private readonly long[] mBytes = new long[256];
        private long mLength;

        public CrcCheckStream(uint crc)
        {
            mExpectedCRC = crc;
            mCurrentCRC = CRC.kInitCRC;
        }

        protected override void Dispose(bool disposing)
        {
            if (mCurrentCRC != mExpectedCRC)
            {
                throw new InvalidOperationException();
            }
            try
            {
                if (disposing && !mClosed)
                {
                    mClosed = true;
                    mCurrentCRC = CRC.Finish(mCurrentCRC);
#if DEBUG
                    if (mCurrentCRC == mExpectedCRC)
                    {
                        Debug.WriteLine("CRC ok: " + mExpectedCRC.ToString("x8"));
                    }
                    else
                    {
                        Debugger.Break();
                        Debug.WriteLine("bad CRC");
                    }

                    double lengthInv = 1.0 / mLength;
                    double entropy = 0;
                    for (int i = 0; i < 256; i++)
                    {
                        if (mBytes[i] != 0)
                        {
                            double p = lengthInv * mBytes[i];
                            entropy -= p * Math.Log(p, 256);
                        }
                    }
                    Debug.WriteLine("entropy: " + (int)(entropy * 100) + "%");
#endif
                }
            }
            finally
            {
                base.Dispose(disposing);
            }
        }

        public override bool CanRead { get { return false; } }

        public override bool CanSeek { get { return false; } }

        public override bool CanWrite { get { return true; } }

        public override void Flush()
        {
        }

        public override long Length { get { throw new NotSupportedException(); } }

        public override long Position { get { throw new NotSupportedException(); } set { throw new NotSupportedException(); } }

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
            mLength += count;
            for (int i = 0; i < count; i++)
            {
                mBytes[buffer[offset + i]]++;
            }

            mCurrentCRC = CRC.Update(mCurrentCRC, buffer, offset, count);
        }
    }
}