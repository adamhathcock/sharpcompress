using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using SharpCompress.Common;
using SharpCompress.Common.Tar;
using SharpCompress.Compressors;
using SharpCompress.Compressors.BZip2;
using SharpCompress.Compressors.Deflate;
using SharpCompress.Compressors.LZMA;
using SharpCompress.Compressors.Lzw;
using SharpCompress.Compressors.Xz;
using SharpCompress.Compressors.ZStandard;
using SharpCompress.IO;

namespace SharpCompress.Readers.Tar;

public partial class TarReader : AbstractReader<TarEntry, TarVolume>
{
    private readonly CompressionType compressionType;

    internal TarReader(Stream stream, ReaderOptions options, CompressionType compressionType)
        : base(options, ArchiveType.Tar)
    {
        this.compressionType = compressionType;
        Volume = new TarVolume(stream, options);
    }

    public override TarVolume Volume { get; }

    protected override Stream RequestInitialStream()
    {
        var stream = base.RequestInitialStream();

        var providers = Options.Providers;

        return compressionType switch
        {
            CompressionType.BZip2 => providers.CreateDecompressStream(
                CompressionType.BZip2,
                stream
            ),
            CompressionType.GZip => providers.CreateDecompressStream(CompressionType.GZip, stream),
            CompressionType.ZStandard => providers.CreateDecompressStream(
                CompressionType.ZStandard,
                stream
            ),
            CompressionType.LZip => providers.CreateDecompressStream(CompressionType.LZip, stream),
            CompressionType.Xz => providers.CreateDecompressStream(CompressionType.Xz, stream),
            CompressionType.Lzw => providers.CreateDecompressStream(CompressionType.Lzw, stream),
            CompressionType.None => stream,
            _ => throw new NotSupportedException("Invalid compression type: " + compressionType),
        };
    }

    protected override ValueTask<Stream> RequestInitialStreamAsync(
        CancellationToken cancellationToken = default
    )
    {
        var stream = base.RequestInitialStream();
        var providers = Options.Providers;

        return compressionType switch
        {
            CompressionType.BZip2 => providers.CreateDecompressStreamAsync(
                CompressionType.BZip2,
                stream,
                cancellationToken
            ),
            CompressionType.GZip => providers.CreateDecompressStreamAsync(
                CompressionType.GZip,
                stream,
                cancellationToken
            ),
            CompressionType.ZStandard => providers.CreateDecompressStreamAsync(
                CompressionType.ZStandard,
                stream,
                cancellationToken
            ),
            CompressionType.LZip => providers.CreateDecompressStreamAsync(
                CompressionType.LZip,
                stream,
                cancellationToken
            ),
            CompressionType.Xz => providers.CreateDecompressStreamAsync(
                CompressionType.Xz,
                stream,
                cancellationToken
            ),
            CompressionType.Lzw => providers.CreateDecompressStreamAsync(
                CompressionType.Lzw,
                stream,
                cancellationToken
            ),
            CompressionType.None => new ValueTask<Stream>(stream),
            _ => throw new NotSupportedException("Invalid compression type: " + compressionType),
        };
    }

    protected override IEnumerable<TarEntry> GetEntries(Stream stream) =>
        TarEntry.GetEntries(
            StreamingMode.Streaming,
            stream,
            compressionType,
            Options.ArchiveEncoding,
            Options
        );
}
