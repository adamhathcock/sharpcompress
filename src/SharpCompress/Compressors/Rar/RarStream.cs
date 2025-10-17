

using System;
using System.Buffers;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using SharpCompress.Common.Rar.Headers;
using SharpCompress.IO;

namespace SharpCompress.Compressors.Rar;

internal class RarStream : Stream, IStreamStack
{
#if DEBUG_STREAMS
    long IStreamStack.InstanceId { get; set; }
#endif
    int IStreamStack.DefaultBufferSize { get; set; }

    Stream IStreamStack.BaseStream() => readStream;

    int IStreamStack.BufferSize
    {
        get => 0;
        set { }
    }
    int IStreamStack.BufferPosition
    {
        get => 0;
        set { }
    }

    void IStreamStack.SetPosition(long position) { }

    private readonly IRarUnpack unpack;
    private readonly FileHeader fileHeader;
    private readonly Stream readStream;

    private bool fetch;

    private byte[]? tmpBuffer = ArrayPool<byte>.Shared.Rent(65536);
    private int tmpOffset;
    private int tmpCount;

    private byte[]? outBuffer;
    private int outOffset;
    private int outCount;
    private int outTotal;
    private bool isDisposed;
    private long _position;

    public static async ValueTask<RarStream> Create(IRarUnpack unpack, FileHeader fileHeader, Stream readStream)
    {
        var rs = new RarStream(unpack, fileHeader, readStream);
        await Initialize(rs, unpack, fileHeader, readStream);
        return rs;
    }

    internal static async ValueTask Initialize(RarStream rs,IRarUnpack unpack, FileHeader fileHeader, Stream readStream)
    {


        rs.fetch = true;
#if !NETSTANDARD2_0 && !NETFRAMEWORK
        await unpack.DoUnpackAsync(fileHeader, readStream, rs);
#else
        unpack.DoUnpack(fileHeader, readStream, rs);
        await Task.CompletedTask;
#endif
        rs.fetch = false;
        rs._position = 0;
    }

    protected RarStream(IRarUnpack unpack, FileHeader fileHeader, Stream readStream)
    {
        this.unpack = unpack;
        this.fileHeader = fileHeader;
        this.readStream = readStream;

#if DEBUG_STREAMS
        this.DebugConstruct(typeof(RarStream));
#endif
    }

    protected override void Dispose(bool disposing)
    {
        if (!isDisposed)
        {
            if (disposing)
            {
#if DEBUG_STREAMS
                this.DebugDispose(typeof(RarStream));
#endif
                if (tmpBuffer != null)
                {
                    ArrayPool<byte>.Shared.Return(this.tmpBuffer);
                    this.tmpBuffer = null;
                }
            }
            isDisposed = true;
            base.Dispose(disposing);
            readStream.Dispose();
        }
    }
#if !NETSTANDARD2_0 && !NETFRAMEWORK
    public override async ValueTask DisposeAsync()
    {
        if (!isDisposed)
        {
#if DEBUG_STREAMS
            this.DebugDispose(typeof(RarStream));
#endif
            if (tmpBuffer != null)
            {
                ArrayPool<byte>.Shared.Return(this.tmpBuffer);
                this.tmpBuffer = null;
            }
            isDisposed = true;
            await readStream.DisposeAsync().ConfigureAwait(false);
        }
        await base.DisposeAsync().ConfigureAwait(false);
    }
#endif

    public override bool CanRead => true;

    public override bool CanSeek => false;

    public override bool CanWrite => false;

    public override void Flush() { }

    public override Task FlushAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    public override long Length => fileHeader.UncompressedSize;

    //commented out code always returned the length of the file
    public override long Position
    {
        get => _position; /* fileHeader.UncompressedSize - unpack.DestSize;*/
        set => throw new NotSupportedException();
    }

#if !NETSTANDARD2_0 && !NETFRAMEWORK

    public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        try
        {
            var bytesRead = Read(buffer, offset, count);
            return Task.FromResult(bytesRead);
        }
        catch (Exception ex)
        {
            return Task.FromException<int>(ex);
        }
    }
    public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

            outTotal = 0;
            var count = buffer.Length;
            var offset = 0;

            if (tmpCount > 0)
            {
                var toCopy = tmpCount < count ? tmpCount : count;
                tmpBuffer.AsSpan(tmpOffset, toCopy).CopyTo(buffer.Span.Slice(offset, toCopy));
                tmpOffset += toCopy;
                tmpCount -= toCopy;
                offset += toCopy;
                count -= toCopy;
                outTotal += toCopy;
            }
            if (count > 0 && unpack.DestSize > 0)
            {
                // Create a temporary array for the unpack operation
                var tempArray = ArrayPool<byte>.Shared.Rent(count);
                try
                {
                    outBuffer = tempArray;
                    outOffset = 0;
                    outCount = count;
                    fetch = true;
                    await unpack.DoUnpackAsync();
                    fetch = false;

                    // Copy the unpacked data to the memory buffer
                    var unpacked = outTotal - (tmpCount > 0 ? offset : 0);
                    if (unpacked > 0)
                    {
                        tempArray.AsSpan(0, unpacked).CopyTo(buffer.Span.Slice(offset, unpacked));
                    }
                }
                finally
                {
                    ArrayPool<byte>.Shared.Return(tempArray);
                    outBuffer = null;
                }
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

    public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException("Use ReadAsync or ReadAsync(Memory<byte>) instead.");
#else


    public override int Read(byte[] buffer, int offset, int count)
    {
        if (tmpBuffer == null)
        {
            throw new ObjectDisposedException(nameof(RarStream));
        }
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
#endif

    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

    public override void SetLength(long value) => throw new NotSupportedException();

    public override void Write(byte[] buffer, int offset, int count)
    {
        if (tmpBuffer == null)
        {
            throw new ObjectDisposedException(nameof(RarStream));
        }
        if (outBuffer == null)
        {
            throw new ObjectDisposedException(nameof(RarStream));
        }
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

    public override Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        try
        {
            Write(buffer, offset, count);
            return Task.CompletedTask;
        }
        catch (Exception ex)
        {
            return Task.FromException(ex);
        }
    }
#if !NETSTANDARD2_0 && !NETFRAMEWORK
    public override ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        try
        {
            if (!fetch)
            {
                throw new NotSupportedException();
            }

            var count = buffer.Length;
            var offset = 0;

            if (outCount > 0)
            {
                var toCopy = outCount < count ? outCount : count;
                buffer.Span.Slice(offset, toCopy).CopyTo(outBuffer.AsSpan(outOffset, toCopy));
                outOffset += toCopy;
                outCount -= toCopy;
                offset += toCopy;
                count -= toCopy;
                outTotal += toCopy;
            }
            if (count > 0)
            {
                EnsureBufferCapacity(count);
                buffer.Span.Slice(offset, count).CopyTo(tmpBuffer.AsSpan(tmpCount, count));
                tmpCount += count;
                tmpOffset = 0;
                unpack.Suspended = true;
            }
            else
            {
                unpack.Suspended = false;
            }
            return ValueTask.CompletedTask;
        }
        catch (Exception ex)
        {
            return new ValueTask(Task.FromException(ex));
        }
    }
#endif

    private void EnsureBufferCapacity(int count)
    {
        if (tmpBuffer == null)
        {
            throw new ObjectDisposedException(nameof(RarStream));
        }
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
