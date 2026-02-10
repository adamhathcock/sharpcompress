using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using SharpCompress.IO;

namespace SharpCompress.Compressors.BZip2;

public sealed partial class BZip2Stream
{
    /// <summary>
    /// Create a BZip2Stream asynchronously
    /// </summary>
    /// <param name="stream">The stream to read from</param>
    /// <param name="compressionMode">Compression Mode</param>
    /// <param name="decompressConcatenated">Decompress Concatenated</param>
    /// <param name="cancellationToken">Cancellation Token</param>
    public static async ValueTask<BZip2Stream> CreateAsync(
        Stream stream,
        CompressionMode compressionMode,
        bool decompressConcatenated,
        bool leaveOpen = false,
        CancellationToken cancellationToken = default
    )
    {
        var bZip2Stream = new BZip2Stream(leaveOpen);
        bZip2Stream.Mode = compressionMode;
        if (bZip2Stream.Mode == CompressionMode.Compress)
        {
            bZip2Stream.stream = new CBZip2OutputStream(stream);
        }
        else
        {
            bZip2Stream.stream = await CBZip2InputStream
                .CreateAsync(stream, decompressConcatenated, leaveOpen, cancellationToken)
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
}
