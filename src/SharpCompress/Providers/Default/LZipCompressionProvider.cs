using System.IO;
using System.Threading;
using System.Threading.Tasks;
using SharpCompress.Common;
using SharpCompress.Compressors;
using SharpCompress.Compressors.LZMA;

namespace SharpCompress.Providers.Default;

/// <summary>
/// Provides LZip compression using SharpCompress's internal implementation.
/// </summary>
public sealed class LZipCompressionProvider : CompressionProviderBase
{
    public override CompressionType CompressionType => CompressionType.LZip;
    public override bool SupportsCompression => true;
    public override bool SupportsDecompression => true;

    public override Stream CreateCompressStream(Stream destination, int compressionLevel)
    {
        return LZipStream.Create(destination, CompressionMode.Compress);
    }

    public override async ValueTask<Stream> CreateCompressStreamAsync(
        Stream destination,
        int compressionLevel,
        CancellationToken cancellationToken = default
    ) =>
        await LZipStream
            .CreateAsync(
                destination,
                CompressionMode.Compress,
                cancellationToken: cancellationToken
            )
            .ConfigureAwait(false);

    public override Stream CreateDecompressStream(Stream source)
    {
        return LZipStream.Create(source, CompressionMode.Decompress);
    }

    public override async ValueTask<Stream> CreateDecompressStreamAsync(
        Stream source,
        CancellationToken cancellationToken = default
    ) =>
        await LZipStream
            .CreateAsync(source, CompressionMode.Decompress, cancellationToken: cancellationToken)
            .ConfigureAwait(false);
}
