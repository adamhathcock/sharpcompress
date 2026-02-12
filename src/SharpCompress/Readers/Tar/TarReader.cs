using System;
using System.Collections.Generic;
using System.IO;
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
        return compressionType switch
        {
            CompressionType.BZip2 => BZip2Stream.Create(stream, CompressionMode.Decompress, false),
            CompressionType.GZip => new GZipStream(stream, CompressionMode.Decompress),
            CompressionType.ZStandard => new ZStandardStream(stream),
            CompressionType.LZip => new LZipStream(stream, CompressionMode.Decompress),
            CompressionType.Xz => new XZStream(stream),
            CompressionType.Lzw => new LzwStream(stream),
            CompressionType.None => stream,
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
