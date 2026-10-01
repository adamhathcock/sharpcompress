using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using SharpCompress.Common;
using SharpCompress.Compressors;
using SharpCompress.Compressors.Deflate;

namespace SharpCompress.Providers.Default;

/// <summary>
/// Provides GZip compression using SharpCompress's internal implementation.
/// </summary>
public sealed partial class GZipCompressionProvider : CompressionProviderBase
{
    public override CompressionType CompressionType => CompressionType.GZip;
    public override bool SupportsCompression => true;
    public override bool SupportsDecompression => true;

    public override Stream CreateCompressStream(Stream destination, int compressionLevel)
    {
        var level = (CompressionLevel)compressionLevel;
        return new GZipStream(destination, CompressionMode.Compress, level, Encoding.UTF8);
    }

    public override Stream CreateDecompressStream(Stream source)
    {
        return new GZipStream(source, CompressionMode.Decompress);
    }

    [Zomp.SyncMethodGenerator.CreateSyncVersion]
    public override async ValueTask<Stream> CreateDecompressStreamAsync(
        Stream source,
        CompressionContext context,
        CancellationToken cancellationToken = default
    )
    {
        cancellationToken.ThrowIfCancellationRequested();
        return await Task.FromResult<Stream>(
                new GZipStream(
                    source,
                    CompressionMode.Decompress,
                    CompressionLevel.Default,
                    context.ResolveArchiveEncoding()
                )
            )
            .ConfigureAwait(false);
    }
}
