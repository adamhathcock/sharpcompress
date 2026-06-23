using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace SharpCompress.Compressors.BZip2;

public sealed partial class BZip2Stream : IAsyncDisposable
{
    /// <summary>
    /// Asynchronously finalizes the BZip2 compressed stream, flushing all pending data.
    /// Use this instead of <see cref="IFinishable.Finish"/> when writing to an async-only stream.
    /// </summary>
    public async ValueTask FinishAsync(CancellationToken cancellationToken = default)
    {
        if (stream is CBZip2OutputStream output)
        {
            await output.FinishAsync(cancellationToken).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Create a BZip2Stream asynchronously
    /// </summary>
    /// <param name="stream">The stream to read from</param>
    /// <param name="compressionMode">Compression Mode</param>
    /// <param name="decompressConcatenated">Decompress Concatenated</param>
    /// <param name="leaveOpen">Leave the underlying stream open when this stream is disposed</param>
    /// <param name="tolerateTruncatedStream">
    /// Decompression only. When true, an end-of-stream reached at a bzip2 block boundary is treated as a
    /// normal end of stream rather than throwing. This allows decoding a truncated or partial stream - for
    /// example a sub-range of blocks extracted for random access - that has no trailing stream footer. EOF
    /// in the middle of a block is still reported as an error. Because a partial decode's running combined
    /// CRC won't match the whole-stream value stored in the footer, that whole-stream CRC is not verified
    /// in this mode (per-block CRCs are still checked).
    /// </param>
    /// <param name="cancellationToken">Cancellation Token</param>
    public static async ValueTask<BZip2Stream> CreateAsync(
        Stream stream,
        CompressionMode compressionMode,
        bool decompressConcatenated,
        bool leaveOpen = false,
        bool tolerateTruncatedStream = false,
        CancellationToken cancellationToken = default
    ) =>
        await CreateAsync(
                stream,
                compressionMode,
                new BZip2StreamOptions
                {
                    DecompressConcatenated = decompressConcatenated,
                    LeaveStreamOpen = leaveOpen,
                    TolerateTruncatedStream = tolerateTruncatedStream,
                },
                cancellationToken
            )
            .ConfigureAwait(false);

    /// <summary>
    /// Create a BZip2Stream asynchronously
    /// </summary>
    /// <param name="stream">The stream to read from</param>
    /// <param name="compressionMode">Compression Mode</param>
    /// <param name="options">BZip2 stream options</param>
    /// <param name="cancellationToken">Cancellation Token</param>
    public static async ValueTask<BZip2Stream> CreateAsync(
        Stream stream,
        CompressionMode compressionMode,
        BZip2StreamOptions options,
        CancellationToken cancellationToken = default
    )
    {
        options = options ?? throw new ArgumentNullException(nameof(options));
        var bZip2Stream = new BZip2Stream();
        bZip2Stream.Mode = compressionMode;
        if (bZip2Stream.Mode == CompressionMode.Compress)
        {
            bZip2Stream.stream = new CBZip2OutputStream(stream, options);
        }
        else
        {
            bZip2Stream.stream = await CBZip2InputStream
                .CreateAsync(stream, options, cancellationToken)
                .ConfigureAwait(false);
        }

        return bZip2Stream;
    }

    /// <summary>
    /// Asynchronously consumes two bytes to test if there is a BZip2 header
    /// </summary>
    /// <param name="stream"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    public static async ValueTask<bool> IsBZip2Async(
        Stream stream,
        CancellationToken cancellationToken = default
    )
    {
        cancellationToken.ThrowIfCancellationRequested();
        var buffer = new byte[2];
        var bytesRead = await stream
            .ReadAsync(buffer, 0, 2, cancellationToken)
            .ConfigureAwait(false);
        if (bytesRead < 2 || buffer[0] != 'B' || buffer[1] != 'Z')
        {
            return false;
        }
        return true;
    }

#if !LEGACY_DOTNET
    public override async ValueTask<int> ReadAsync(
        Memory<byte> buffer,
        CancellationToken cancellationToken = default
    ) => await stream.ReadAsync(buffer, cancellationToken).ConfigureAwait(false);

    public override async ValueTask WriteAsync(
        ReadOnlyMemory<byte> buffer,
        CancellationToken cancellationToken = default
    ) => await stream.WriteAsync(buffer, cancellationToken).ConfigureAwait(false);
#endif

    public override async Task<int> ReadAsync(
        byte[] buffer,
        int offset,
        int count,
        CancellationToken cancellationToken = default
    ) => await stream.ReadAsync(buffer, offset, count, cancellationToken).ConfigureAwait(false);

    public override async Task WriteAsync(
        byte[] buffer,
        int offset,
        int count,
        CancellationToken cancellationToken = default
    ) => await stream.WriteAsync(buffer, offset, count, cancellationToken).ConfigureAwait(false);

#if !LEGACY_DOTNET || NETSTANDARD2_1
    public override async ValueTask DisposeAsync()
#else
    public async ValueTask DisposeAsync()
#endif
    {
        if (isDisposed)
        {
            return;
        }

        isDisposed = true;
        if (stream is IAsyncDisposable asyncDisposable)
        {
            await asyncDisposable.DisposeAsync().ConfigureAwait(false);
        }
        else
        {
            stream.Dispose();
        }

#if !LEGACY_DOTNET || NETSTANDARD2_1
        await base.DisposeAsync().ConfigureAwait(false);
#else
        await Task.CompletedTask.ConfigureAwait(false);
#endif
        GC.SuppressFinalize(this);
    }
}
