using System;
using SharpCompress.Common;
using SharpCompress.Compressors.Deflate;
using SharpCompress.Writers;

namespace SharpCompress.Writers.Zip;

public class ZipWriterEntryOptions
{
    private CompressionType? compressionType;
    private int? compressionLevel;

    public CompressionType? CompressionType
    {
        get => compressionType;
        set
        {
            if (value.HasValue && compressionLevel.HasValue)
            {
                CompressionLevelValidation.Validate(value.Value, compressionLevel.Value);
            }
            compressionType = value;
        }
    }

    /// <summary>
    /// The compression level to be used when the compression type supports variable levels.
    /// Valid ranges depend on the compression algorithm:
    /// - Deflate/GZip: 0-9 (0=no compression, 6=default, 9=best compression)
    /// - ZStandard: 1-22 (1=fastest, 3=default, 22=best compression)
    /// When null, uses the archive's default compression level for the specified compression type.
    /// Note: BZip2 and LZMA do not support compression levels in this implementation.
    /// </summary>
    public int? CompressionLevel
    {
        get => compressionLevel;
        set
        {
            if (value.HasValue && compressionType.HasValue)
            {
                CompressionLevelValidation.Validate(compressionType.Value, value.Value);
            }
            compressionLevel = value;
        }
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

    internal void ValidateWithFallback(CompressionType fallbackCompressionType, int fallbackLevel)
    {
        CompressionLevelValidation.Validate(
            CompressionType ?? fallbackCompressionType,
            CompressionLevel ?? fallbackLevel
        );
    }
}
