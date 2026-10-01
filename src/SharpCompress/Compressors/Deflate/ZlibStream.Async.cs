using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using SharpCompress.IO;

namespace SharpCompress.Compressors.Deflate;

public partial class ZlibStream
{
    /// <summary>
    /// Flush the stream.
    /// </summary>
    [Zomp.SyncMethodGenerator.CreateSyncVersion]
    public override async Task FlushAsync(CancellationToken cancellationToken)
    {
        if (_disposed)
        {
            throw new ObjectDisposedException("ZlibStream");
        }
        await _baseStream.FlushAsync(cancellationToken).ConfigureAwait(false);
    }

#if !LEGACY_DOTNET
    public override async ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return;
        }
        _disposed = true;
        if (_baseStream != null)
        {
            await _baseStream.DisposeAsync().ConfigureAwait(false);
        }
        await base.DisposeAsync().ConfigureAwait(false);
    }
#endif

    /// <summary>
    /// Read data from the stream.
    /// </summary>
    /// <remarks>
    /// A <c>ZlibStream</c> compresses or decompresses according to its
    /// <c>CompressionMode</c>. It can be used for <c>Read()</c> or <c>Write()</c>, but not both.
    /// </remarks>
    /// <param name="buffer">The buffer into which the read data should be placed.</param>
    /// <param name="offset">The offset within that data array to put the first byte read.</param>
    /// <param name="count">The number of bytes to read.</param>
    /// <returns>The number of bytes actually read.</returns>
    [Zomp.SyncMethodGenerator.CreateSyncVersion]
    public override async Task<int> ReadAsync(
        byte[] buffer,
        int offset,
        int count,
        CancellationToken cancellationToken
    )
    {
        if (_disposed)
        {
            throw new ObjectDisposedException("ZlibStream");
        }
        return await _baseStream
            .ReadAsync(buffer, offset, count, cancellationToken)
            .ConfigureAwait(false);
    }

#if !LEGACY_DOTNET
    public override async ValueTask<int> ReadAsync(
        Memory<byte> buffer,
        CancellationToken cancellationToken = default
    )
    {
        if (_disposed)
        {
            throw new ObjectDisposedException("ZlibStream");
        }
        return await _baseStream.ReadAsync(buffer, cancellationToken).ConfigureAwait(false);
    }
#endif

    /// <summary>
    /// Write data to the stream.
    /// </summary>
    /// <remarks>
    /// A <c>ZlibStream</c> compresses or decompresses according to its
    /// <c>CompressionMode</c>. It can be used for <c>Read()</c> or <c>Write()</c>, but not both.
    /// </remarks>
    /// <param name="buffer">The buffer holding data to write to the stream.</param>
    /// <param name="offset">The offset within that data array to find the first byte to write.</param>
    /// <param name="count">The number of bytes to write.</param>
    [Zomp.SyncMethodGenerator.CreateSyncVersion]
    public override async Task WriteAsync(
        byte[] buffer,
        int offset,
        int count,
        CancellationToken cancellationToken
    )
    {
        if (_disposed)
        {
            throw new ObjectDisposedException("ZlibStream");
        }
        await _baseStream
            .WriteAsync(buffer, offset, count, cancellationToken)
            .ConfigureAwait(false);
    }

#if !LEGACY_DOTNET
    public override async ValueTask WriteAsync(
        ReadOnlyMemory<byte> buffer,
        CancellationToken cancellationToken = default
    )
    {
        if (_disposed)
        {
            throw new ObjectDisposedException("ZlibStream");
        }
        await _baseStream.WriteAsync(buffer, cancellationToken).ConfigureAwait(false);
    }
#endif
}
