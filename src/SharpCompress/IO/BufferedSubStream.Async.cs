using System;
using System.Buffers;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace SharpCompress.IO;

internal partial class BufferedSubStream
{
    [Zomp.SyncMethodGenerator.CreateSyncVersion]
    private async ValueTask RefillCacheAsync(CancellationToken cancellationToken)
    {
        if (_isDisposed)
        {
            throw new ObjectDisposedException(nameof(BufferedSubStream));
        }

        var count = (int)Math.Min(BytesLeftToRead, _cache!.Length);
        _cacheOffset = 0;
        if (count == 0)
        {
            _cacheLength = 0;
            return;
        }
        // Only seek if we're not already at the correct position
        // This avoids expensive seek operations when reading sequentially
        if (_stream.CanSeek && _stream.Position != origin)
        {
            _stream.Position = origin;
        }
        _cacheLength = await _stream
            .ReadAsync(_cache, 0, count, cancellationToken)
            .ConfigureAwait(false);
        origin += _cacheLength;
        BytesLeftToRead -= _cacheLength;
    }

    // Fast, synchronous-completion path used when the requested bytes are already sitting in the
    // in-memory cache. Avoids the async state-machine / Task allocation overhead incurred by every
    // single-byte read the LZMA range decoder issues, without changing buffering/caching semantics.
    public override Task<int> ReadAsync(
        byte[] buffer,
        int offset,
        int count,
        CancellationToken cancellationToken
    )
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (count > Length)
        {
            count = (int)Length;
        }

        if (count > 0 && _cacheOffset < _cacheLength)
        {
            count = Math.Min(count, _cacheLength - _cacheOffset);
            Buffer.BlockCopy(_cache!, _cacheOffset, buffer, offset, count);
            _cacheOffset += count;
            return Task.FromResult(count);
        }

        return ReadSlowAsync(buffer, offset, count, cancellationToken);
    }

    private async Task<int> ReadSlowAsync(
        byte[] buffer,
        int offset,
        int count,
        CancellationToken cancellationToken
    )
    {
        if (count > 0)
        {
            if (_cacheOffset == _cacheLength)
            {
                await RefillCacheAsync(cancellationToken).ConfigureAwait(false);
            }

            count = Math.Min(count, _cacheLength - _cacheOffset);
            Buffer.BlockCopy(_cache!, _cacheOffset, buffer, offset, count);
            _cacheOffset += count;
        }

        return count;
    }

#if !LEGACY_DOTNET
    public override ValueTask<int> ReadAsync(
        Memory<byte> buffer,
        CancellationToken cancellationToken = default
    )
    {
        cancellationToken.ThrowIfCancellationRequested();

        var count = buffer.Length;
        if (count > Length)
        {
            count = (int)Length;
        }

        if (count > 0 && _cacheOffset < _cacheLength)
        {
            count = Math.Min(count, _cacheLength - _cacheOffset);
            _cache!.AsSpan(_cacheOffset, count).CopyTo(buffer.Span);
            _cacheOffset += count;
            return new ValueTask<int>(count);
        }

        return ReadSlowAsync(buffer, cancellationToken);
    }

    private async ValueTask<int> ReadSlowAsync(
        Memory<byte> buffer,
        CancellationToken cancellationToken
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
            _cache!.AsSpan(_cacheOffset, count).CopyTo(buffer.Span);
            _cacheOffset += count;
        }

        return count;
    }
#endif
}
