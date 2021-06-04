using System;
using System.IO;

namespace SharpCompress.IO
{
    internal class SeekableSubStream : NonDisposingStream
    {
        private readonly long _origin;
        private long _pos;

        public override bool CanRead => true;
        public override bool CanWrite => false;
        public override bool CanSeek => true;
        public override long Length { get; }

        public override long Position
        {
            get => _pos;
            set
            {
                if ((value < 0) || (value > Length)) throw new ArgumentOutOfRangeException(nameof(value));
                _pos = value;
            }
        }

        public SeekableSubStream(Stream stream, long origin, long length)
            : base(stream, false)
        {
            if (!stream.CanRead) throw new ArgumentException("Requires a readable stream", nameof(stream));
            if (!stream.CanSeek) throw new ArgumentException("Requires a seekable stream", nameof(stream));

            _origin = origin;
            Length = length;
            _pos = 0;
        }

        public override void Flush()
        { }

        public override int Read(byte[] buffer, int offset, int count)
        {
            count = (int)Math.Min(count, Length - Position);

            Stream.Position = Position + _origin;
            count = Stream.Read(buffer, offset, count);

            Position += count;
            return count;
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            long ClampPos(long value) => Math.Min(Math.Max(value, 0), Length);

            switch (origin)
            {
                case SeekOrigin.Begin:
                    Position = ClampPos(offset);
                    break;

                case SeekOrigin.Current:
                    Position = ClampPos(Position + offset);
                    break;

                case SeekOrigin.End:
                    Position = ClampPos(Length - offset);
                    break;
            }

            return Position;
        }

        public override void SetLength(long value)
            => throw new NotSupportedException();

        public override void Write(byte[] buffer, int offset, int count)
            => throw new NotSupportedException();
    }
}
