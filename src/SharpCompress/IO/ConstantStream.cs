using System;
using System.IO;

namespace SharpCompress.IO
{
    internal class ConstantStream : Stream
    {
        private long _length;
        private long _pos;

        public byte Value { get; set; }

        public override bool CanRead => true;
        public override bool CanWrite => false;
        public override bool CanSeek => true;
        public override long Length => _length;

        public override long Position
        {
            get => _pos;
            set
            {
                if ((value < 0) || (value > Length)) throw new ArgumentOutOfRangeException(nameof(value));
                _pos = value;
            }
        }

        public ConstantStream(byte value, long length)
        {
            Value = value;
            _length = length;
            _pos = 0;
        }

        private long ClampPos(long value) => Math.Min(Math.Max(value, 0), Length);

        public override void Flush()
        { }

        public override int Read(byte[] buffer, int offset, int count)
        {
            count = (int)Math.Min(count, Length - Position);
            for (int i = 0; i < count; i++)
                buffer[i + offset] = Value;
            Position += count;
            return count;
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
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
        {
            _length = value;
            Position = ClampPos(Position);
        }

        public override void Write(byte[] buffer, int offset, int count)
            => throw new NotSupportedException();

        protected override void Dispose(bool disposing)
        { }
    }
}
