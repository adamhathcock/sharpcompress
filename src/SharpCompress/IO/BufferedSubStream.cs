using System;
using System.IO;

namespace SharpCompress.IO;

internal class BufferedSubStream : SharpCompressStream, IStreamStack
{
#if DEBUG_STREAMS
    long IStreamStack.InstanceId { get; set; }
#endif

    Stream IStreamStack.BaseStream() => base.Stream;

    public BufferedSubStream(Stream stream, long origin, long bytesToRead)
        : base(stream, leaveOpen: true, throwOnDispose: false)
    {
#if DEBUG_STREAMS
        this.DebugConstruct(typeof(BufferedSubStream));
#endif
        this.origin = origin;
        this.BytesLeftToRead = bytesToRead;
    }

    protected override void Dispose(bool disposing)
    {
#if DEBUG_STREAMS
        this.DebugDispose(typeof(BufferedSubStream));
#endif
        if (disposing) { }
        base.Dispose(disposing);
    }

    private int _cacheOffset;
    private int _cacheLength;
    private readonly byte[] _cache = new byte[32 << 10];
    private long origin;

    private long BytesLeftToRead { get; set; }

    public override bool CanRead => true;

    public override bool CanSeek => false;

    public override bool CanWrite => false;

    public override void Flush() { }

    public override long Length => BytesLeftToRead + _cacheLength - _cacheOffset;

    public override long Position
    {
        get => throw new NotSupportedException();
        set => throw new NotSupportedException();
    }

    private void RefillCache()
    {
        var count = (int)Math.Min(BytesLeftToRead, _cache.Length);
        _cacheOffset = 0;
        if (count == 0)
        {
            _cacheLength = 0;
            return;
        }
        Stream.Position = origin;
        _cacheLength = Stream.Read(_cache, 0, count);
        origin += _cacheLength;
        BytesLeftToRead -= _cacheLength;
    }

    public override int Read(byte[] buffer, int offset, int count)
    {
        if (count > Length)
        {
            count = (int)Length;
        }

        if (count > 0)
        {
            if (_cacheOffset == _cacheLength)
            {
                RefillCache();
            }

            count = Math.Min(count, _cacheLength - _cacheOffset);
            Buffer.BlockCopy(_cache, _cacheOffset, buffer, offset, count);
            _cacheOffset += count;
        }

        return count;
    }

    public override int ReadByte()
    {
        if (_cacheOffset == _cacheLength)
        {
            RefillCache();
            if (_cacheLength == 0)
            {
                return -1;
            }
        }

        return _cache[_cacheOffset++];
    }

    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

    public override void SetLength(long value) => throw new NotSupportedException();

    public override void Write(byte[] buffer, int offset, int count) =>
        throw new NotSupportedException();
}
