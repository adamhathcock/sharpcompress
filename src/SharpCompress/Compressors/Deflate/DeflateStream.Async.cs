using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using SharpCompress.IO;

namespace SharpCompress.Compressors.Deflate;

public partial class DeflateStream
{
#if !LEGACY_DOTNET || NETSTANDARD2_1
    public override async ValueTask DisposeAsync()
#else
    public async ValueTask DisposeAsync()
#endif
    {
        if (!_disposed)
        {
            if (!_leaveOpen)
            {
                await _baseStream.DisposeAsync().ConfigureAwait(false);
            }
            _disposed = true;
        }
#if !LEGACY_DOTNET || NETSTANDARD2_1
        await base.DisposeAsync().ConfigureAwait(false);
#else
        await Task.CompletedTask.ConfigureAwait(false);
#endif
    }

    /// <summary>
    /// Flush the stream.
    /// </summary>
    [Zomp.SyncMethodGenerator.CreateSyncVersion]
    public override async Task FlushAsync(CancellationToken cancellationToken)
    {
        if (_disposed)
        {
            throw new ObjectDisposedException("DeflateStream");
        }
        await _baseStream.FlushAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Read data from the stream.
    /// </summary>
    /// <remarks>
    /// <para>
    /// If you wish to use the <c>DeflateStream</c> to compress data while reading, create it with
    /// <c>CompressionMode.Compress</c> and an uncompressed data stream. Reading then returns
    /// compressed data. With <c>CompressionMode.Decompress</c>, reading returns decompressed data.
    /// </para>
    /// <para>
    /// A <c>DeflateStream</c> can be used for <c>Read()</c> or <c>Write()</c>, but not both.
    /// </para>
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
            throw new ObjectDisposedException("DeflateStream");
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
            throw new ObjectDisposedException("DeflateStream");
        }
        return await _baseStream.ReadAsync(buffer, cancellationToken).ConfigureAwait(false);
    }
#endif

    /// <summary>
    /// Write data to the stream.
    /// </summary>
    /// <remarks>
    /// <para>
    /// With <c>CompressionMode.Compress</c>, written uncompressed data is compressed to the
    /// destination. With <c>CompressionMode.Decompress</c>, written compressed data is decompressed
    /// to the destination.
    /// </para>
    /// <para>
    /// A <c>DeflateStream</c> can be used for <c>Read()</c> or <c>Write()</c>, but not both.
    /// </para>
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
            throw new ObjectDisposedException("DeflateStream");
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
            throw new ObjectDisposedException("DeflateStream");
        }
        await _baseStream.WriteAsync(buffer, cancellationToken).ConfigureAwait(false);
    }
#endif
}
