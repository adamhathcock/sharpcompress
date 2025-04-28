#nullable disable

using System;
using System.Buffers;
using System.IO;
using SharpCompress.Common.Rar.Headers;

namespace SharpCompress.Compressors.Rar;

internal class RarStream : Stream
{
    private readonly IRarUnpack unpack;
    private readonly FileHeader fileHeader;
    private readonly Stream readStream;

    private bool fetch;

    private byte[] tmpBuffer = ArrayPool<byte>.Shared.Rent(65536);
    private int tmpOffset;
    private int tmpCount;

    private byte[] outBuffer;
    private int outOffset;
    private int outCount;
    private int outTotal;
    private bool isDisposed;
    private long _position;

    public RarStream(IRarUnpack unpack, FileHeader fileHeader, Stream readStream)
    {
        this.unpack = unpack;
        this.fileHeader = fileHeader;
        this.readStream = readStream;
        fetch = true;
        unpack.DoUnpack(fileHeader, readStream, this);
        fetch = false;
        _position = 0;
    }

    protected override void Dispose(bool disposing)
    {
        if (!isDisposed)
        {
            if (disposing)
            {
                ArrayPool<byte>.Shared.Return(this.tmpBuffer);
                this.tmpBuffer = null;
            }
            isDisposed = true;
            base.Dispose(disposing);
            readStream.Dispose();
        }
    }

    public override bool CanRead => true;

    public override bool CanSeek => false;

    public override bool CanWrite => false;

    public override void Flush() { }

    public override long Length => fileHeader.UncompressedSize;

    //commented out code always returned the length of the file
    public override long Position
    {
        get => _position; /* fileHeader.UncompressedSize - unpack.DestSize;*/
        set => throw new NotSupportedException();
    }

    public override int Read(byte[] buffer, int offset, int count)
    {
        outTotal = 0;
        if (tmpCount > 0)
        {
            var toCopy = tmpCount < count ? tmpCount : count;
            Buffer.BlockCopy(tmpBuffer, tmpOffset, buffer, offset, toCopy);
            tmpOffset += toCopy;
            tmpCount -= toCopy;
            offset += toCopy;
            count -= toCopy;
            outTotal += toCopy;
        }
        if (count > 0 && unpack.DestSize > 0)
        {
            outBuffer = buffer;
            outOffset = offset;
            outCount = count;
            fetch = true;
            unpack.DoUnpack();
            fetch = false;
        }
        _position += outTotal;
        if (count > 0 && outTotal == 0 && _position != Length)
        {
            // sanity check, eg if we try to decompress a redir entry
            throw new InvalidOperationException(
                $"unpacked file size does not match header: expected {Length} found {_position}"
            );
        }
        return outTotal;
    }

    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

    public override void SetLength(long value) => throw new NotSupportedException();

    public override void Write(byte[] buffer, int offset, int count)
    {
        if (!fetch)
        {
            throw new NotSupportedException();
        }
        if (outCount > 0)
        {
            var toCopy = outCount < count ? outCount : count;
            Buffer.BlockCopy(buffer, offset, outBuffer, outOffset, toCopy);
            outOffset += toCopy;
            outCount -= toCopy;
            offset += toCopy;
            count -= toCopy;
            outTotal += toCopy;
        }
        if (count > 0)
        {
            EnsureBufferCapacity(count);
            Buffer.BlockCopy(buffer, offset, tmpBuffer, tmpCount, count);
            tmpCount += count;
            tmpOffset = 0;
            unpack.Suspended = true;
        }
        else
        {
            unpack.Suspended = false;
        }
    }

    private void EnsureBufferCapacity(int count)
    {
        if (this.tmpBuffer.Length < this.tmpCount + count)
        {
            var newLength =
                this.tmpBuffer.Length * 2 > this.tmpCount + count
                    ? this.tmpBuffer.Length * 2
                    : this.tmpCount + count;
            var newBuffer = ArrayPool<byte>.Shared.Rent(newLength);
            Buffer.BlockCopy(this.tmpBuffer, 0, newBuffer, 0, this.tmpCount);
            var oldBuffer = this.tmpBuffer;
            this.tmpBuffer = newBuffer;
            ArrayPool<byte>.Shared.Return(oldBuffer);
        }
    }
}
