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
        if (_isRewound && _bufferPosition != _bufferLength)
        {
            read = ReadFromBuffer(buffer, offset, count);
            if (read < count)
            {
                int tempRead = await stream
                    .ReadAsync(buffer, offset + read, count - read, cancellationToken)
                    .ConfigureAwait(false);
                if (IsRecording)
                {
                    WriteToBuffer(buffer, offset + read, tempRead);
                }
                read += tempRead;
            }
            if (_bufferPosition == _bufferLength)
            {
                _isRewound = false;
                _bufferPosition = 0;
                if (!IsRecording)
                {
                    _bufferLength = 0;
                }
            }
            return read;
        }

        read = await stream
            .ReadAsync(buffer, offset, count, cancellationToken)
            .ConfigureAwait(false);
        if (IsRecording)
        {
            WriteToBuffer(buffer, offset, read);
            _bufferPosition = _bufferLength;
        }
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
        if (_isRewound && _bufferPosition != _bufferLength)
        {
            var bufferSpan = buffer.Span;
            read = ReadFromBuffer(bufferSpan);
            if (read < bufferSpan.Length)
            {
                int tempRead = await stream
                    .ReadAsync(buffer.Slice(read), cancellationToken)
                    .ConfigureAwait(false);
                if (IsRecording)
                {
                    WriteToBuffer(buffer.Slice(read, tempRead).Span);
                }
                read += tempRead;
            }
            if (_bufferPosition == _bufferLength)
            {
                _isRewound = false;
                _bufferPosition = 0;
                if (!IsRecording)
                {
                    _bufferLength = 0;
                }
            }
            return read;
        }

        read = await stream.ReadAsync(buffer, cancellationToken).ConfigureAwait(false);
        if (IsRecording)
        {
            WriteToBuffer(buffer.Slice(0, read).Span);
            _bufferPosition = _bufferLength;
        }
        return read;
    }
#endif
}
