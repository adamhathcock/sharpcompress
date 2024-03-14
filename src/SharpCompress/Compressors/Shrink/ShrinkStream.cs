using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SharpCompress.Compressors.Shrink;

internal class ShrinkStream : Stream
{
    private Stream inStream;
    private CompressionMode _compressionMode;

    private ulong _compressedSize;
    private long _uncompressedSize;
    private byte[] _byteOut;
    private long _outBytesCount;

    public ShrinkStream(
        Stream stream,
        CompressionMode compressionMode,
        long compressedSize,
        long uncompressedSize
    )
    {
        inStream = stream;
        _compressionMode = compressionMode;

        _compressedSize = (ulong)compressedSize;
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
        get => _outBytesCount;
        set => throw new NotImplementedException();
    }

    public override void Flush() => throw new NotImplementedException();

    public override int Read(byte[] buffer, int offset, int count)
    {
        if (inStream.Position == (long)_compressedSize)
        {
            return 0;
        }
        var src = new byte[_compressedSize];
        inStream.Read(src, offset, (int)_compressedSize);
        var srcUsed = 0;
        var dstUsed = 0;

        HwUnshrink.Unshrink(
            src,
            (int)_compressedSize,
            out srcUsed,
            _byteOut,
            (int)_uncompressedSize,
            out dstUsed
        );
        _outBytesCount = _byteOut.Length;

        for (var index = 0; index < _outBytesCount; ++index)
        {
            buffer[offset + index] = _byteOut[index];
        }
        var tmp = _outBytesCount;
        _outBytesCount = 0;
        return (int)tmp;
    }

    public override long Seek(long offset, SeekOrigin origin) =>
        throw new NotImplementedException();

    public override void SetLength(long value) => throw new NotImplementedException();

    public override void Write(byte[] buffer, int offset, int count) =>
        throw new NotImplementedException();
}
