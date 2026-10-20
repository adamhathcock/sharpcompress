using System;
using System.Buffers;
using System.IO;
using SharpCompress.Common;
using SharpCompress.IO;

namespace SharpCompress.Compressors.Shrink;

internal partial class ShrinkStream : Stream
{
    private readonly Stream _inStream;
    private readonly ArrayPool<byte> _pool;

    private readonly long _uncompressedSize;
    private byte[] _byteOut = Array.Empty<byte>();
    private long _outBytesCount;
    private bool _decompressed;
    private bool _disposed;
    private long _position;

    public ShrinkStream(Stream stream, long uncompressedSize)
        : this(stream, uncompressedSize, ArrayPool<byte>.Shared) { }

    internal ShrinkStream(Stream stream, long uncompressedSize, ArrayPool<byte> pool)
    {
        if (uncompressedSize < 0 || uncompressedSize > int.MaxValue)
        {
            throw new InvalidFormatException(
                $"Shrink: declared uncompressed size {uncompressedSize} exceeds maximum supported size."
            );
        }

        _inStream = stream;
        _pool = pool;

        _uncompressedSize = uncompressedSize;
        _outBytesCount = 0L;
    }

    public override bool CanRead => !_disposed;

    public override bool CanSeek => true;

    public override bool CanWrite => false;

    public override long Length => _uncompressedSize;

    public override long Position
    {
        get => _position;
        set => throw new NotImplementedException();
    }

    public override void Flush() => throw new NotImplementedException();

    public override int Read(byte[] buffer, int offset, int count)
    {
        ThrowIfDisposed();
        if (!_decompressed)
        {
            // Read actual compressed data from the stream rather than pre-allocating based on the
            // declared compressed size, which may be crafted to cause an OutOfMemoryException.
            // The stream is already bounded by ReadOnlySubStream in ZipFilePart.
            using var srcMs = new PooledMemoryStream();
            _inStream.CopyTo(srcMs);
            Decode(srcMs);
        }

        long remaining = _outBytesCount - _position;
        if (remaining <= 0)
        {
            return 0;
        }

        int toCopy = (int)Math.Min(count, remaining);
        Buffer.BlockCopy(_byteOut, (int)_position, buffer, offset, toCopy);
        _position += toCopy;
        return toCopy;
    }

    public override long Seek(long offset, SeekOrigin origin) =>
        throw new NotImplementedException();

    private void Decode(PooledMemoryStream source)
    {
        var length = checked((int)source.Length);
        var src = _pool.Rent(length);
        var output = Array.Empty<byte>();
        try
        {
            source.Position = 0;
            if (source.Read(src, 0, length) != length)
            {
                throw new InvalidFormatException("Shrink: truncated compressed data.");
            }
            // A pool rental may be larger than the compressed data; never decode its unused tail.
            var status = HwUnshrink.UnshrinkGrowing(
                src,
                length,
                (int)_uncompressedSize,
                _pool,
                out output,
                out var used
            );
            if (status != HwUnshrink.UnshrnkStatus.Ok || used != _uncompressedSize)
            {
                throw new InvalidFormatException("Shrink: invalid or truncated compressed data.");
            }
            _byteOut = output;
            output = Array.Empty<byte>();
            _outBytesCount = used;
            _decompressed = true;
            _position = 0;
        }
        finally
        {
            _pool.Return(src);
            if (output.Length > 0)
            {
                _pool.Return(output);
            }
        }
    }

    protected override void Dispose(bool disposing)
    {
        if (!_disposed)
        {
            _disposed = true;
            var output = _byteOut;
            _byteOut = Array.Empty<byte>();
            if (output.Length > 0)
            {
                _pool.Return(output);
            }
        }
        base.Dispose(disposing);
    }

    private void ThrowIfDisposed()
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(nameof(ShrinkStream));
        }
    }

    public override void SetLength(long value) => throw new NotImplementedException();

    public override void Write(byte[] buffer, int offset, int count) =>
        throw new NotImplementedException();
}
