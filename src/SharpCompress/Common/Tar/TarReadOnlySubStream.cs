using System;
using System.IO;
using SharpCompress.IO;

namespace SharpCompress.Common.Tar;

internal class TarReadOnlySubStream : SharpCompressStream, IStreamStack
{
#if DEBUG_STREAMS
    long IStreamStack.InstanceId { get; set; }
#endif

    Stream IStreamStack.BaseStream() => base.Stream;

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

    private bool _isDisposed;
    private long _amountRead;

    public TarReadOnlySubStream(Stream stream, long bytesToRead)
        : base(stream, leaveOpen: true, throwOnDispose: false)
    {
        BytesLeftToRead = bytesToRead;
#if DEBUG_STREAMS
        this.DebugConstruct(typeof(TarReadOnlySubStream));
#endif
    }

    protected override void Dispose(bool disposing)
    {
        if (_isDisposed)
        {
            return;
        }

        _isDisposed = true;
#if DEBUG_STREAMS
        this.DebugDispose(typeof(TarReadOnlySubStream));
#endif
        if (disposing)
        {
            // Ensure we read all remaining blocks for this entry.
            Stream.Skip(BytesLeftToRead);
            _amountRead += BytesLeftToRead;

            // If the last block wasn't a full 512 bytes, skip the remaining padding bytes.
            var bytesInLastBlock = _amountRead % 512;

            if (bytesInLastBlock != 0)
            {
                Stream.Skip(512 - bytesInLastBlock);
            }
        }

        base.Dispose(disposing);
    }

#if !NETFRAMEWORK && !NETSTANDARD2_0
    public override async System.Threading.Tasks.ValueTask DisposeAsync()
    {
        if (_isDisposed)
        {
            return;
        }

        _isDisposed = true;
#if DEBUG_STREAMS
        this.DebugDispose(typeof(TarReadOnlySubStream));
#endif
        // Ensure we read all remaining blocks for this entry.
        await Stream.SkipAsync(BytesLeftToRead).ConfigureAwait(false);
        _amountRead += BytesLeftToRead;

        // If the last block wasn't a full 512 bytes, skip the remaining padding bytes.
        var bytesInLastBlock = _amountRead % 512;

        if (bytesInLastBlock != 0)
        {
            await Stream.SkipAsync(512 - bytesInLastBlock).ConfigureAwait(false);
        }

        // Call base Dispose instead of base DisposeAsync to avoid double disposal
        base.Dispose(true);
        GC.SuppressFinalize(this);
    }
#endif

    private long BytesLeftToRead { get; set; }

    public override bool CanRead => true;

    public override bool CanSeek => false;

    public override bool CanWrite => false;

    public override void Flush() { }

    public override System.Threading.Tasks.Task FlushAsync(
        System.Threading.CancellationToken cancellationToken
    ) => System.Threading.Tasks.Task.CompletedTask;

    public override long Length => throw new NotSupportedException();

    public override long Position
    {
        get => throw new NotSupportedException();
        set => throw new NotSupportedException();
    }

    public override int Read(byte[] buffer, int offset, int count)
    {
        if (BytesLeftToRead < count)
        {
            count = (int)BytesLeftToRead;
        }
        var read = Stream.Read(buffer, offset, count);
        if (read > 0)
        {
            BytesLeftToRead -= read;
            _amountRead += read;
        }
        return read;
    }

    public override int ReadByte()
    {
        if (BytesLeftToRead <= 0)
        {
            return -1;
        }
        var value = Stream.ReadByte();
        if (value != -1)
        {
            --BytesLeftToRead;
            ++_amountRead;
        }
        return value;
    }

    public override async System.Threading.Tasks.Task<int> ReadAsync(
        byte[] buffer,
        int offset,
        int count,
        System.Threading.CancellationToken cancellationToken
    )
    {
        if (BytesLeftToRead < count)
        {
            count = (int)BytesLeftToRead;
        }
        var read = await Stream
            .ReadAsync(buffer, offset, count, cancellationToken)
            .ConfigureAwait(false);
        if (read > 0)
        {
            BytesLeftToRead -= read;
            _amountRead += read;
        }
        return read;
    }

#if !NETFRAMEWORK && !NETSTANDARD2_0
    public override async System.Threading.Tasks.ValueTask<int> ReadAsync(
        System.Memory<byte> buffer,
        System.Threading.CancellationToken cancellationToken = default
    )
    {
        if (BytesLeftToRead < buffer.Length)
        {
            buffer = buffer.Slice(0, (int)BytesLeftToRead);
        }
        var read = await Stream.ReadAsync(buffer, cancellationToken).ConfigureAwait(false);
        if (read > 0)
        {
            BytesLeftToRead -= read;
            _amountRead += read;
        }
        return read;
    }
#endif

    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

    public override void SetLength(long value) => throw new NotSupportedException();

    public override void Write(byte[] buffer, int offset, int count) =>
        throw new NotSupportedException();
}
