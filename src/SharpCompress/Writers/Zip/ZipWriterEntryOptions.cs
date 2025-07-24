using System;
using SharpCompress.Common;
using SharpCompress.Compressors.Deflate;

namespace SharpCompress.Writers.Zip;

public class ZipWriterEntryOptions
{
    public CompressionType? CompressionType { get; set; }

    /// <summary>
    /// The compression level to be used when the compression type supports variable levels.
    /// Valid ranges depend on the compression algorithm:
    /// - Deflate/GZip: 0-9 (0=no compression, 6=default, 9=best compression)
    /// - ZStandard: 1-22 (1=fastest, 3=default, 22=best compression)
    /// When null, uses the archive's default compression level for the specified compression type.
    /// Note: BZip2 and LZMA do not support compression levels in this implementation.
    /// </summary>
    public int? CompressionLevel { get; set; }

    /// <summary>
    /// When CompressionType.Deflate is used, this property is referenced.
    /// Valid range: 0-9 (0=no compression, 6=default, 9=best compression).
    /// When null, uses the archive's default compression level.
    /// </summary>
    /// <remarks>
    /// This property is deprecated. Use <see cref="CompressionLevel"/> instead.
    /// </remarks>
    [Obsolete(
        "Use CompressionLevel property instead. This property will be removed in a future version."
    )]
    public CompressionLevel? DeflateCompressionLevel
    {
        get =>
            CompressionLevel.HasValue
                ? (CompressionLevel)Math.Min(CompressionLevel.Value, 9)
                : null;
        set => CompressionLevel = value.HasValue ? (int)value.Value : null;
    }

    public string? EntryComment { get; set; }

    public DateTime? ModificationDateTime { get; set; }

    /// <summary>
    /// Allocate an extra 20 bytes for this entry to store,
    /// 64 bit length values, thus enabling streams
    /// larger than 4GiB.
    /// This option is not supported with non-seekable streams.
    /// </summary>
    public bool? EnableZip64 { get; set; }
}
