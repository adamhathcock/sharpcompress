using System;
using SharpCompress.Common;
using D = SharpCompress.Compressors.Deflate;

namespace SharpCompress.Writers;

public class WriterOptions : OptionsBase
{
    public WriterOptions(CompressionType compressionType)
    {
        CompressionType = compressionType;
        CompressionLevel = compressionType switch
        {
            CompressionType.ZStandard => 3,
            CompressionType.Deflate => (int)D.CompressionLevel.Default,
            CompressionType.Deflate64 => (int)D.CompressionLevel.Default,
            CompressionType.GZip => (int)D.CompressionLevel.Default,
            _ => 0,
        };
    }

    public WriterOptions(CompressionType compressionType, int compressionLevel)
    {
        CompressionType = compressionType;
        CompressionLevel = compressionLevel;
    }

    public CompressionType CompressionType { get; set; }

    /// <summary>
    /// The compression level to be used when the compression type supports variable levels.
    /// Valid ranges depend on the compression algorithm:
    /// - Deflate/GZip: 0-9 (0=no compression, 6=default, 9=best compression)
    /// - ZStandard: 1-22 (1=fastest, 3=default, 22=best compression)
    /// Note: BZip2 and LZMA do not support compression levels in this implementation.
    /// Defaults are set automatically based on compression type in the constructor.
    /// </summary>
    public int CompressionLevel { get; set; }

    /// <summary>
    /// An optional progress reporter for tracking compression operations.
    /// When set, progress updates will be reported as entries are written.
    /// </summary>
    public IProgress<ProgressReport>? Progress { get; set; }

    public static implicit operator WriterOptions(CompressionType compressionType) =>
        new(compressionType);
}
