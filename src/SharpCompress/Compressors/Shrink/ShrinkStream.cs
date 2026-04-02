using System;
using System.IO;

namespace SharpCompress.Compressors.Shrink;

internal partial class ShrinkStream : Stream
{
    private readonly Stream _inStream;

    private readonly long _uncompressedSize;
    private readonly byte[] _byteOut;
    private long _outBytesCount;
    private bool _decompressed;
    private long _position;

    public ShrinkStream(Stream stream, long uncompressedSize)
    {
        _inStream = stream;

        _uncompressedSize = uncompressedSize;
        _byteOut = new byte[_uncompressedSize];
        _outBytesCount = 0L;
    }

    public override bool CanRead => true;

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
        if (!_decompressed)
        {
            // Read actual compressed data from the stream rather than pre-allocating based on the
            // declared compressed size, which may be crafted to cause an OutOfMemoryException.
            // The stream is already bounded by ReadOnlySubStream in ZipFilePart.
            using var srcMs = new MemoryStream();
            _inStream.CopyTo(srcMs);
            var src = srcMs.ToArray();
            var srcLen = src.Length;

            HwUnshrink.Unshrink(
                src,
                srcLen,
                out _,
                _byteOut,
                (int)_uncompressedSize,
                out var dstUsed
            );
            _outBytesCount = dstUsed;
            _decompressed = true;
            _position = 0;
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

    public override void SetLength(long value) => throw new NotImplementedException();

    public override void Write(byte[] buffer, int offset, int count) =>
        throw new NotImplementedException();
}
