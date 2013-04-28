using System;
using System.IO;

namespace SharpCompress.Compressor.LZMA.Utilites
{
    /// <summary>
    /// Allows reading the same stream from multiple threads by synchronizing read access.
    /// </summary>
    internal class SyncStreamView : Stream
    {
        private object mSync;
        private Stream mStream;
        private long mOrigin;
        private long mEnding;
        private long mOffset;

        public SyncStreamView(object sync, Stream stream, long origin, long length)
        {
            mSync = sync;
            mStream = stream;
            mOrigin = origin;
            mEnding = checked(origin + length);
            mOffset = 0;
        }

        public override bool CanRead
        {
            get { return true; }
        }

        public override bool CanSeek
        {
            get { return true; }
        }

        public override bool CanWrite
        {
            get { return false; }
        }

        public override void Flush()
        {
            throw new InvalidOperationException();
        }

        public override long Length
        {
            get { return mEnding - mOrigin; }
        }

        public override long Position
        {
            get { return mOffset; }
            set
            {
                if (value < 0 || value > Length)
                    throw new ArgumentOutOfRangeException("value");

                mOffset = value;
            }
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            long remaining = mEnding - mOrigin - mOffset;
            if (count > remaining)
                count = (int) remaining;

            if (count == 0)
                return 0;

            int delta;
            lock (mSync)
            {
                mStream.Position = mOrigin + mOffset;
                delta = mStream.Read(buffer, offset, count);
            }

            mOffset += delta;
            return delta;
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            switch (origin)
            {
                case SeekOrigin.Begin:
                    return Position = offset;
                case SeekOrigin.Current:
                    return Position += offset;
                case SeekOrigin.End:
                    return Position = Length + offset;
                default:
                    throw new ArgumentOutOfRangeException("origin");
            }
        }

        public override void SetLength(long value)
        {
            throw new InvalidOperationException();
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            throw new InvalidOperationException();
        }
    }
}