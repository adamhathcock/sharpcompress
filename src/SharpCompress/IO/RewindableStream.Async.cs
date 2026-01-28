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
        //don't actually read if we don't really want to read anything
        //currently a network stream bug on Windows for .NET Core
        if (count == 0)
        {
            return 0;
        }
        int read;
        if (_isRewound && _bufferStream.Position != _bufferStream.Length)
        {
            read = await _bufferStream
                .ReadAsync(buffer, offset, count, cancellationToken)
                .ConfigureAwait(false);
            if (read < count)
            {
                int tempRead = await stream
                    .ReadAsync(buffer, offset + read, count - read, cancellationToken)
                    .ConfigureAwait(false);
                if (IsRecording)
                {
                    await _bufferStream
                        .WriteAsync(buffer, offset + read, tempRead, cancellationToken)
                        .ConfigureAwait(false);
                }
                read += tempRead;
            }
            if (_bufferStream.Position == _bufferStream.Length && !IsRecording)
            {
                _isRewound = false;
                _bufferStream.SetLength(0);
            }
            return read;
        }

        read = await stream
            .ReadAsync(buffer, offset, count, cancellationToken)
            .ConfigureAwait(false);
        if (IsRecording)
        {
            await _bufferStream
                .WriteAsync(buffer, offset, read, cancellationToken)
                .ConfigureAwait(false);
        }
        return read;
    }

#if !LEGACY_DOTNET
    public override async ValueTask<int> ReadAsync(
        Memory<byte> buffer,
        CancellationToken cancellationToken = default
    )
    {
        //don't actually read if we don't really want to read anything
        //currently a network stream bug on Windows for .NET Core
        if (buffer.Length == 0)
        {
            return 0;
        }
        int read;
        if (_isRewound && _bufferStream.Position != _bufferStream.Length)
        {
            var bufferSpan = buffer.Span;
            read = _bufferStream.Read(bufferSpan);
            if (read < bufferSpan.Length)
            {
                int tempRead = await stream
                    .ReadAsync(buffer.Slice(read), cancellationToken)
                    .ConfigureAwait(false);
                if (IsRecording)
                {
                    await _bufferStream
                        .WriteAsync(buffer.Slice(read, tempRead), cancellationToken)
                        .ConfigureAwait(false);
                }
                read += tempRead;
            }
            if (_bufferStream.Position == _bufferStream.Length && !IsRecording)
            {
                _isRewound = false;
                _bufferStream.SetLength(0);
            }
            return read;
        }

        read = await stream.ReadAsync(buffer, cancellationToken).ConfigureAwait(false);
        if (IsRecording)
        {
            await _bufferStream.WriteAsync(buffer.Slice(0, read), cancellationToken).ConfigureAwait(false);
        }
        return read;
    }
#endif
}
