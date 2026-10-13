using System;
using System.Buffers;
using System.IO;
using System.Runtime.ExceptionServices;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;

namespace SharpCompress.Common.Zip;

internal partial class WinzipAesCryptoStream
{
#if !LEGACY_DOTNET
    public override async ValueTask DisposeAsync()
    {
        if (_isDisposed)
        {
            await base.DisposeAsync().ConfigureAwait(false);
            return;
        }
        _isDisposed = true;
        try
        {
            if (!_authRead)
            {
                await DrainAndVerifyAsync().ConfigureAwait(false);
            }
        }
        finally
        {
            _authentication.Dispose();
            _transform.Dispose();
            _cipher.Dispose();
            await _stream.DisposeAsync().ConfigureAwait(false);
        }
        await base.DisposeAsync().ConfigureAwait(false);
    }
#endif

    private async ValueTask VerifyAuthenticationAsync(CancellationToken cancellationToken)
    {
        _authenticationFailure?.Throw();
        if (_authRead)
        {
            return;
        }
        try
        {
            while (_authenticationTagBytesRead < _authenticationTag.Length)
            {
                var read = await _stream
                    .ReadAsync(
                        _authenticationTag,
                        _authenticationTagBytesRead,
                        _authenticationTag.Length - _authenticationTagBytesRead,
                        cancellationToken
                    )
                    .ConfigureAwait(false);
                if (read == 0)
                {
                    throw new IncompleteArchiveException(
                        "Unexpected end of WinZip AES authentication tag."
                    );
                }
                _authenticationTagBytesRead += read;
            }
            ValidateAuthentication(_authenticationTag);
            _authRead = true;
        }
        catch (OperationCanceledException)
        {
            // Cancellation is transient; keep the tag prefix so disposal can finish verification.
            throw;
        }
        catch (Exception exception)
        {
            _authRead = true;
            _authenticationFailure = ExceptionDispatchInfo.Capture(exception);
            throw;
        }
    }

    private async ValueTask DrainAndVerifyAsync()
    {
        var buffer = new byte[4096];
        while (_totalBytesLeftToRead > 0)
        {
            await ReadAsync(buffer, 0, buffer.Length, CancellationToken.None).ConfigureAwait(false);
        }
        await VerifyAuthenticationAsync(CancellationToken.None).ConfigureAwait(false);
    }

    [Zomp.SyncMethodGenerator.CreateSyncVersion]
    public override async Task<int> ReadAsync(
        byte[] buffer,
        int offset,
        int count,
        CancellationToken cancellationToken
    )
    {
        cancellationToken.ThrowIfCancellationRequested();
        _authenticationFailure?.Throw();
        if (count == 0)
        {
            return 0;
        }
        if (_totalBytesLeftToRead == 0)
        {
            await VerifyAuthenticationAsync(cancellationToken).ConfigureAwait(false);
            return 0;
        }
        var bytesToRead = count;
        if (count > _totalBytesLeftToRead)
        {
            bytesToRead = (int)_totalBytesLeftToRead;
        }
        var read = await _stream
            .ReadAsync(buffer, offset, bytesToRead, cancellationToken)
            .ConfigureAwait(false);
        if (read == 0)
        {
            _authRead = true;
            throw new IncompleteArchiveException("Unexpected end of WinZip AES ciphertext.");
        }
        _authentication.TransformBlock(buffer, offset, read, null, 0);
        _totalBytesLeftToRead -= read;
        if (_totalBytesLeftToRead == 0)
        {
            await VerifyAuthenticationAsync(cancellationToken).ConfigureAwait(false);
        }

        ReadTransformBlocks(buffer, offset, read);

        return read;
    }

#if !LEGACY_DOTNET
    public override async ValueTask<int> ReadAsync(
        Memory<byte> buffer,
        CancellationToken cancellationToken = default
    )
    {
        if (MemoryMarshal.TryGetArray((ReadOnlyMemory<byte>)buffer, out var segment))
        {
            return await ReadAsync(segment.Array!, segment.Offset, segment.Count, cancellationToken)
                .ConfigureAwait(false);
        }
        var rented = ArrayPool<byte>.Shared.Rent(buffer.Length);
        try
        {
            var read = await ReadAsync(rented, 0, buffer.Length, cancellationToken)
                .ConfigureAwait(false);
            rented.AsSpan(0, read).CopyTo(buffer.Span);
            return read;
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(rented, clearArray: true);
        }
    }
#endif
}
