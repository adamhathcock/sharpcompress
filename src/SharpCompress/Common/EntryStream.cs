using System;
using System.IO;
using SharpCompress.Reader;

namespace SharpCompress.Common
{
    public class EntryStream : Stream
    {
        public IReader Reader { get; private set; }
        private Stream stream;
        private bool completed;
        private bool isDisposed;

        internal EntryStream(IReader reader, Stream stream)
        {
            this.Reader = reader;
            this.stream = stream;
        }

        /// <summary>
        /// When reading a stream from OpenEntryStream, the stream must be completed so use this to finish reading the entire entry.
        /// </summary>
        public void SkipEntry()
        {
            var buffer = new byte[4096];
            while (Read(buffer, 0, buffer.Length) > 0)
            {
            }
            completed = true;
        }

        protected override void Dispose(bool disposing)
        {
            if (!(completed || Reader.Cancelled))
            {
                SkipEntry();
            }
            if (isDisposed)
            {
                return;
            }
            isDisposed = true;
            base.Dispose(disposing);
            stream.Dispose();
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

        public override void Flush()
        {
            throw new NotSupportedException();
        }

        public override long Length
        {
            get { throw new NotSupportedException(); }
        }

        public override long Position
        {
            get { throw new NotSupportedException(); }
            set { throw new NotSupportedException(); }
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            int read = stream.Read(buffer, offset, count);
            if (read <= 0)
            {
                completed = true;
            }
            return read;
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