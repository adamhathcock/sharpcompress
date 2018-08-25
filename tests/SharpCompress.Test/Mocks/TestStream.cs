using System.IO;

namespace SharpCompress.Test.Mocks
{
    public class TestStream : Stream
    {
        private readonly Stream stream;

        public TestStream(Stream stream) : this(stream, stream.CanRead, stream.CanWrite, stream.CanSeek)
        {
        }

        public bool IsDisposed { get; private set; }

        public TestStream(Stream stream, bool read, bool write, bool seek)
        {
            this.stream = stream;
            CanRead = read;
            CanWrite = write;
            CanSeek = seek;
        }

        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);
            stream.Dispose();
            IsDisposed = true;
        }

        public override bool CanRead { get; }

        public override bool CanSeek { get; }

        public override bool CanWrite { get; }

        public override void Flush()
        {
            stream.Flush();
        }

        public override long Length => stream.Length;

        public override long Position
        {
            get => stream.Position;
            set => stream.Position = value;
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            return stream.Read(buffer, offset, count);
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            return stream.Seek(offset, origin);
        }

        public override void SetLength(long value)
        {
            stream.SetLength(value);
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            stream.Write(buffer, offset, count);
        }
    }
}
