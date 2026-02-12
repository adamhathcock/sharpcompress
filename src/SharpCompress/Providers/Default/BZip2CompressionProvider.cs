using System.IO;
using System.Threading;
using System.Threading.Tasks;
using SharpCompress.Common;
using SharpCompress.Compressors;
using SharpCompress.Compressors.BZip2;

namespace SharpCompress.Providers.Default;

/// <summary>
/// Provides BZip2 compression using SharpCompress's internal implementation.
/// </summary>
public sealed class BZip2CompressionProvider : CompressionProviderBase
{
    public override CompressionType CompressionType => CompressionType.BZip2;
    public override bool SupportsCompression => true;
    public override bool SupportsDecompression => true;

    public override Stream CreateCompressStream(Stream destination, int compressionLevel)
    {
        // BZip2 doesn't use compressionLevel parameter in this implementation
        return BZip2Stream.Create(destination, CompressionMode.Compress, false);
    }

    public override Stream CreateCompressStream(
        Stream destination,
        int compressionLevel,
        CompressionContext context
    )
    {
        // Context not used for BZip2 compression
        return CreateCompressStream(destination, compressionLevel);
    }

    public override Stream CreateDecompressStream(Stream source)
    {
        return BZip2Stream.Create(source, CompressionMode.Decompress, false);
    }

    public override Stream CreateDecompressStream(Stream source, CompressionContext context)
    {
        // Context not used for BZip2 decompression
        return CreateDecompressStream(source);
    }

    public override async ValueTask<Stream> CreateDecompressStreamAsync(
        Stream source,
        CancellationToken cancellationToken = default
    )
    {
        return await BZip2Stream
            .CreateAsync(source, CompressionMode.Decompress, false, false, cancellationToken)
            .ConfigureAwait(false);
    }

    public override async ValueTask<Stream> CreateDecompressStreamAsync(
        Stream source,
        CompressionContext context,
        CancellationToken cancellationToken = default
    )
    {
        // Context not used for BZip2 decompression
        return await CreateDecompressStreamAsync(source, cancellationToken).ConfigureAwait(false);
    }
}
