using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace SharpCompress.IO;

internal partial class ReadOnlySubStream
{
    public override async Task<int> ReadAsync(
        byte[] buffer,
        int offset,
        int count,
        CancellationToken cancellationToken
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
            _position += read;
        }
        return read;
    }

#if !LEGACY_DOTNET
    public override async ValueTask<int> ReadAsync(
        Memory<byte> buffer,
        CancellationToken cancellationToken = default
    )
    {
        var sliceLen = BytesLeftToRead < buffer.Length ? BytesLeftToRead : buffer.Length;
        var read = await Stream
            .ReadAsync(buffer.Slice(0, (int)sliceLen), cancellationToken)
            .ConfigureAwait(false);
        if (read > 0)
        {
            BytesLeftToRead -= read;
            _position += read;
        }
        return read;
    }
#endif
}
