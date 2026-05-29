using System;
using System.IO;
using System.Threading.Tasks;

namespace SharpCompress.Common.Tar;

internal class TarReadOnlySubStream : Stream
{
    private readonly Stream _stream;

    private bool _isDisposed;
    private bool _isPositionedAtNextEntry;
    private long _amountRead;

    public TarReadOnlySubStream(Stream stream, long bytesToRead)
    {
        _stream = stream;
        BytesLeftToRead = bytesToRead;
    }

    protected override void Dispose(bool disposing)
    {
        if (_isDisposed)
        {
            base.Dispose(disposing);
            return;
        }

        _isDisposed = true;
        if (disposing)
        {
            if (Utility.UseSyncOverAsyncDispose())
            {
#pragma warning disable VSTHRD002 // Avoid problematic synchronous waits
#pragma warning disable CA2012
                AdvanceToNextHeaderAsync().GetAwaiter().GetResult();
#pragma warning restore CA2012
#pragma warning restore VSTHRD002 // Avoid problematic synchronous waits
            }
            else
            {
                AdvanceToNextHeader();
            }
        }
        base.Dispose(disposing);
    }

#if !LEGACY_DOTNET
    public override async System.Threading.Tasks.ValueTask DisposeAsync()
    {
        if (_isDisposed)
        {
            await base.DisposeAsync().ConfigureAwait(false);
            return;
        }

        _isDisposed = true;
        await AdvanceToNextHeaderAsync().ConfigureAwait(false);

        GC.SuppressFinalize(this);
        await base.DisposeAsync().ConfigureAwait(false);
    }
#endif

    private long BytesLeftToRead { get; set; }

    private void AdvanceToNextHeader()
    {
        if (_isPositionedAtNextEntry)
        {
            return;
        }

        if (BytesLeftToRead > 0)
        {
            _stream.Skip(BytesLeftToRead);
            _amountRead += BytesLeftToRead;
            BytesLeftToRead = 0;
        }

        // Tar entry data is padded to 512-byte blocks, so callers that read to EOF
        // should still leave the shared archive stream positioned at the next header.
        var bytesInLastBlock = _amountRead % 512;
        if (bytesInLastBlock != 0)
        {
            _stream.Skip(512 - bytesInLastBlock);
        }

        _isPositionedAtNextEntry = true;
    }

    private async ValueTask AdvanceToNextHeaderAsync()
    {
        if (_isPositionedAtNextEntry)
        {
            return;
        }

        if (BytesLeftToRead > 0)
        {
            await _stream.SkipAsync(BytesLeftToRead).ConfigureAwait(false);
            _amountRead += BytesLeftToRead;
            BytesLeftToRead = 0;
        }

        var bytesInLastBlock = _amountRead % 512;
        if (bytesInLastBlock != 0)
        {
            await _stream.SkipAsync(512 - bytesInLastBlock).ConfigureAwait(false);
        }

        _isPositionedAtNextEntry = true;
    }

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
        if (BytesLeftToRead <= 0)
        {
            AdvanceToNextHeader();
            return 0;
        }
        if (BytesLeftToRead < count)
        {
            count = (int)BytesLeftToRead;
        }
        var read = _stream.Read(buffer, offset, count);
        if (read > 0)
        {
            BytesLeftToRead -= read;
            _amountRead += read;
            if (BytesLeftToRead == 0)
            {
                AdvanceToNextHeader();
            }
        }
        return read;
    }

    public override int ReadByte()
    {
        if (BytesLeftToRead <= 0)
        {
            AdvanceToNextHeader();
            return -1;
        }
        var value = _stream.ReadByte();
        if (value != -1)
        {
            --BytesLeftToRead;
            ++_amountRead;
            if (BytesLeftToRead == 0)
            {
                AdvanceToNextHeader();
            }
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
        if (BytesLeftToRead <= 0)
        {
            await AdvanceToNextHeaderAsync().ConfigureAwait(false);
            return 0;
        }
        if (BytesLeftToRead < count)
        {
            count = (int)BytesLeftToRead;
        }
        var read = await _stream
            .ReadAsync(buffer, offset, count, cancellationToken)
            .ConfigureAwait(false);
        if (read > 0)
        {
            BytesLeftToRead -= read;
            _amountRead += read;
            if (BytesLeftToRead == 0)
            {
                await AdvanceToNextHeaderAsync().ConfigureAwait(false);
            }
        }
        return read;
    }

#if !LEGACY_DOTNET
    public override async System.Threading.Tasks.ValueTask<int> ReadAsync(
        System.Memory<byte> buffer,
        System.Threading.CancellationToken cancellationToken = default
    )
    {
        if (BytesLeftToRead <= 0)
        {
            await AdvanceToNextHeaderAsync().ConfigureAwait(false);
            return 0;
        }
        if (BytesLeftToRead < buffer.Length)
        {
            buffer = buffer.Slice(0, (int)BytesLeftToRead);
        }
        var read = await _stream.ReadAsync(buffer, cancellationToken).ConfigureAwait(false);
        if (read > 0)
        {
            BytesLeftToRead -= read;
            _amountRead += read;
            if (BytesLeftToRead == 0)
            {
                await AdvanceToNextHeaderAsync().ConfigureAwait(false);
            }
        }
        return read;
    }
#endif

    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

    public override void SetLength(long value) => throw new NotSupportedException();

    public override void Write(byte[] buffer, int offset, int count) =>
        throw new NotSupportedException();
}
