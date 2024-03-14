using System;
using System.IO;

namespace SharpCompress.IO;

internal class BufferedSubStream(Stream stream, long origin, long bytesToRead)
    : NonDisposingStream(stream, throwOnDispose: false)
{
    private int _cacheOffset;
    private int _cacheLength;
    private readonly byte[] _cache = new byte[32 << 10];

    private long BytesLeftToRead { get; set; } = bytesToRead;

    public override bool CanRead => true;

    public override bool CanSeek => false;

    public override bool CanWrite => false;

    public override void Flush() { }

    public override long Length => BytesLeftToRead;

    public override long Position
    {
        get => throw new NotSupportedException();
        set => throw new NotSupportedException();
    }

    public override int Read(byte[] buffer, int offset, int count)
    {
        if (count > BytesLeftToRead)
        {
            count = (int)BytesLeftToRead;
        }

        if (count > 0)
        {
            if (_cacheLength == 0)
            {
                _cacheOffset = 0;
                Stream.Position = origin;
                _cacheLength = Stream.Read(_cache, 0, _cache.Length);
                origin += _cacheLength;
            }

            if (count > _cacheLength)
            {
                count = _cacheLength;
            }

            Buffer.BlockCopy(_cache, _cacheOffset, buffer, offset, count);
            _cacheOffset += count;
            _cacheLength -= count;
            BytesLeftToRead -= count;
        }

        return count;
    }

    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

    public override void SetLength(long value) => throw new NotSupportedException();

    public override void Write(byte[] buffer, int offset, int count) =>
        throw new NotSupportedException();
}
