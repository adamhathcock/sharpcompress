
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace master._7zip.Utilities
{
    /// <remarks>
    /// This stream is a length-constrained wrapper around a cached stream so it does not dispose the inner stream.
    /// </remarks>
    internal class UnpackSubStream: Stream
    {
        private Stream mSource;
        private long mLength;
        private long mOffset;

        internal UnpackSubStream(Stream source, long length)
        {
            mSource = source;
            mLength = length;
        }

        public override bool CanRead
        {
            get { return mSource.CanRead; }
        }

        public override bool CanSeek
        {
            get { return false; }
        }

        public override bool CanWrite
        {
            get { return false; }
        }

        public override void Flush()
        {
            throw new NotSupportedException();
        }

        public override long Length
        {
            get { return mLength; }
        }

        public override long Position
        {
            get { return mOffset; }
            set { throw new NotSupportedException(); }
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            if(buffer == null)
                throw new ArgumentNullException("buffer");

            if(offset < 0 || offset > buffer.Length)
                throw new ArgumentOutOfRangeException("offset");

            if(count < 0 || count > buffer.Length - offset)
                throw new ArgumentOutOfRangeException("count");

            if(count > mLength - mOffset)
                count = (int)(mLength - mOffset);

            if(count == 0)
                return 0;

            int processed = mSource.Read(buffer, offset, count);
            if(processed == 0)
                throw new EndOfStreamException("Decoded stream ended prematurely, unpacked data is corrupt.");

            mOffset += processed;
            return processed;
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
    }
}
