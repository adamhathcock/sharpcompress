using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace SharpCompress.IO;

internal partial class RewindableStream
{
    public override async Task<int> ReadAsync(
        byte[] buffer,
        int offset,
        int count,
        CancellationToken cancellationToken
    )
    {
        if (count == 0)
        {
            return 0;
        }

        int read;
        if (isRewound && bufferStream.Position != bufferStream.Length)
        {
            var readCount = Math.Min(count, (int)(bufferStream.Length - bufferStream.Position));
            read = await bufferStream
                .ReadAsync(buffer, offset, readCount, cancellationToken)
                .ConfigureAwait(false);
            if (read < count)
            {
                var tempRead = await stream
                    .ReadAsync(buffer, offset + read, count - read, cancellationToken)
                    .ConfigureAwait(false);
                if (IsRecording)
                {
                    await bufferStream
                        .WriteAsync(buffer, offset + read, tempRead, cancellationToken)
                        .ConfigureAwait(false);
                }
                streamPosition += tempRead;
                read += tempRead;
            }
            if (bufferStream.Position == bufferStream.Length)
            {
                isRewound = false;
            }
            return read;
        }

        read = await stream
            .ReadAsync(buffer, offset, count, cancellationToken)
            .ConfigureAwait(false);
        if (IsRecording)
        {
            await bufferStream
                .WriteAsync(buffer, offset, read, cancellationToken)
                .ConfigureAwait(false);
        }
        streamPosition += read;
        return read;
    }

#if !LEGACY_DOTNET
    public override async ValueTask<int> ReadAsync(
        Memory<byte> buffer,
        CancellationToken cancellationToken = default
    )
    {
        if (buffer.Length == 0)
        {
            return 0;
        }

        int read;
        if (isRewound && bufferStream.Position != bufferStream.Length)
        {
            var readCount = (int)
                Math.Min(buffer.Length, bufferStream.Length - bufferStream.Position);
            read = await bufferStream
                .ReadAsync(buffer.Slice(0, readCount), cancellationToken)
                .ConfigureAwait(false);
            if (read < buffer.Length)
            {
                var tempRead = await stream
                    .ReadAsync(buffer.Slice(read), cancellationToken)
                    .ConfigureAwait(false);
                if (IsRecording)
                {
                    await bufferStream
                        .WriteAsync(buffer.Slice(read, tempRead), cancellationToken)
                        .ConfigureAwait(false);
                }
                streamPosition += tempRead;
                read += tempRead;
            }
            if (bufferStream.Position == bufferStream.Length)
            {
                isRewound = false;
            }
            return read;
        }

        read = await stream.ReadAsync(buffer, cancellationToken).ConfigureAwait(false);
        if (IsRecording)
        {
            await bufferStream
                .WriteAsync(buffer.Slice(0, read), cancellationToken)
                .ConfigureAwait(false);
        }
        streamPosition += read;
        return read;
    }
#endif

    public override Task WriteAsync(
        byte[] buffer,
        int offset,
        int count,
        CancellationToken cancellationToken
    ) => throw new NotSupportedException();

#if !LEGACY_DOTNET
    public override ValueTask WriteAsync(
        ReadOnlyMemory<byte> buffer,
        CancellationToken cancellationToken = default
    ) => throw new NotSupportedException();
#endif

    public override Task FlushAsync(CancellationToken cancellationToken) =>
        throw new NotSupportedException();

    public override async Task CopyToAsync(
        Stream destination,
        int bufferSize,
        CancellationToken cancellationToken
    )
    {
        byte[] buffer = new byte[bufferSize];
        int bytesRead;
        while ((bytesRead = await ReadAsync(buffer, 0, buffer.Length, cancellationToken)) != 0)
        {
            await destination.WriteAsync(buffer, 0, bytesRead, cancellationToken);
        }
    }

#if !LEGACY_DOTNET
    public override async ValueTask DisposeAsync()
    {
        if (!isDisposed)
        {
            isDisposed = true;
            await stream.DisposeAsync();
        }
    }
#endif
}
