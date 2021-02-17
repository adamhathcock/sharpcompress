using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace SharpCompress.IO
{
    internal class CompositeStream : Stream
    {
        private readonly IReadOnlyList<Stream> _streams;
        private long _pos;
        private int _streamIndex;
        private long _streamPos;

        public override bool CanRead { get; }
        public override bool CanWrite { get; }
        public override bool CanSeek { get; }
        public override long Length { get; }

        public override long Position
        {
            get => _pos;
            set
            {
                if (!CanSeek) throw new NotSupportedException();
                if ((value < 0) || (value > Length)) throw new ArgumentOutOfRangeException(nameof(value));

                _pos = value;

                _streamIndex = -1;
                long offset = _pos;
                for (int i = 0; i < _streams.Count; i++)
                {
                    var stream = _streams[i];
                    if (offset < stream.Length)
                    {
                        _streamIndex = i;
                        _streamPos = offset;
                        break;
                    }
                    else
                    {
                        offset -= stream.Length;
                    }
                }
            }
        }

        public CompositeStream(IReadOnlyList<Stream> streams)
        {
            CanRead = true;
            CanWrite = false;
            CanSeek = true;
            Length = 0;
            _pos = 0;
            _streamIndex = 0;
            _streamPos = 0;

            _streams = streams;
            foreach (var stream in _streams)
            {
                if (!stream.CanRead) throw new ArgumentException("All streams must be readable");
                if (!stream.CanSeek) CanSeek = false;
                Length += stream.Length;
            }
        }

        public CompositeStream(IEnumerable<Stream> streams)
            : this((IReadOnlyList<Stream>)streams.ToArray())
        { }

        public CompositeStream(params Stream[] streams)
            : this((IReadOnlyList<Stream>)streams)
        { }

        public override int Read(byte[] buffer, int offset, int count)
        {
            Stream? GetCurrent()
            {
                if ((_streamIndex < 0) || (_streamIndex >= _streams.Count)) return null;
                else return _streams[_streamIndex];
            }

            if (CanSeek)
            {
                var stream = GetCurrent();
                if (stream is null) return 0;

                stream.Position = _streamPos;
                int readCount = stream.Read(buffer, offset, count);
                _pos += readCount;
                _streamPos += readCount;

                while (readCount < count)
                {
                    _streamIndex++;
                    stream = GetCurrent();
                    if (stream is null) return readCount;

                    stream.Position = _streamPos = 0;
                    int rc = stream.Read(buffer, offset + readCount, count - readCount);
                    readCount += rc;
                    _pos += rc;
                    _streamPos += rc;
                }

                return readCount;
            }
            else
            {
                var stream = GetCurrent();
                if (stream is null) return 0;

                int readCount = stream.Read(buffer, offset, count);
                _pos += readCount;

                while (readCount < count)
                {
                    _streamIndex++;
                    stream = GetCurrent();
                    if (stream is null) return readCount;

                    int rc = stream.Read(buffer, offset + readCount, count - readCount);
                    readCount += rc;
                    _pos += rc;
                }

                return readCount;
            }
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            if (CanSeek)
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
            }
            
            return Position;
        }

        public override void Flush()
        { }

        public override void Write(byte[] buffer, int offset, int count)
            => throw new NotSupportedException();

        public override void SetLength(long value)
            => throw new NotSupportedException();

        protected override void Dispose(bool disposing)
        {
            foreach (var stream in _streams)
                stream.Dispose();
        }
    }
}
