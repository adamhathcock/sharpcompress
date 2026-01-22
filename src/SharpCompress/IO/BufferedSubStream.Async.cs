using System;
using System.Buffers;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace SharpCompress.IO;

internal partial class BufferedSubStream
{
    private async ValueTask RefillCacheAsync(CancellationToken cancellationToken)
    {
        var count = (int)Math.Min(BytesLeftToRead, _cache.Length);
        _cacheOffset = 0;
        if (count == 0)
        {
            _cacheLength = 0;
            return;
        }
        Stream.Position = origin;
        _cacheLength = await Stream
            .ReadAsync(_cache, 0, count, cancellationToken)
            .ConfigureAwait(false);
        origin += _cacheLength;
        BytesLeftToRead -= _cacheLength;
    }

    public override async Task<int> ReadAsync(
        byte[] buffer,
        int offset,
        int count,
        CancellationToken cancellationToken
    )
    {
        if (count > Length)
        {
            count = (int)Length;
        }

        if (count > 0)
        {
            if (_cacheOffset == _cacheLength)
            {
                await RefillCacheAsync(cancellationToken).ConfigureAwait(false);
            }

            count = Math.Min(count, _cacheLength - _cacheOffset);
            Buffer.BlockCopy(_cache, _cacheOffset, buffer, offset, count);
            _cacheOffset += count;
        }

        return count;
    }

#if !LEGACY_DOTNET
    public override async ValueTask<int> ReadAsync(
        Memory<byte> buffer,
        CancellationToken cancellationToken = default
    )
    {
        var count = buffer.Length;
        if (count > Length)
        {
            count = (int)Length;
        }

        if (count > 0)
        {
            if (_cacheOffset == _cacheLength)
            {
                await RefillCacheAsync(cancellationToken).ConfigureAwait(false);
            }

            count = Math.Min(count, _cacheLength - _cacheOffset);
            _cache.AsSpan(_cacheOffset, count).CopyTo(buffer.Span);
            _cacheOffset += count;
        }

        return count;
    }
#endif
}
