using System;
using SharpCompress.Common;
using SharpCompress.Common.Options;
using D = SharpCompress.Compressors.Deflate;

namespace SharpCompress.Writers;

/// <summary>
/// Options for configuring writer behavior when creating archives.
/// </summary>
/// <remarks>
/// This class is immutable. Use the <c>with</c> expression to create modified copies:
/// <code>
/// var options = new WriterOptions(CompressionType.Zip);
/// options = options with { LeaveStreamOpen = false };
/// </code>
/// </remarks>
public sealed record WriterOptions : IWriterOptions
{
    /// <summary>
    /// The compression type to use for the archive.
    /// </summary>
    public CompressionType CompressionType { get; init; }

    /// <summary>
    /// The compression level to be used when the compression type supports variable levels.
    /// Valid ranges depend on the compression algorithm:
    /// - Deflate/GZip: 0-9 (0=no compression, 6=default, 9=best compression)
    /// - ZStandard: 1-22 (1=fastest, 3=default, 22=best compression)
    /// Note: BZip2 and LZMA do not support compression levels in this implementation.
    /// Defaults are set automatically based on compression type in the constructor.
    /// </summary>
    public int CompressionLevel { get; init; }

    /// <summary>
    /// SharpCompress will keep the supplied streams open.  Default is true.
    /// </summary>
    public bool LeaveStreamOpen { get; init; } = true;

    /// <summary>
    /// Encoding to use for archive entry names.
    /// </summary>
    public IArchiveEncoding ArchiveEncoding { get; init; } = new ArchiveEncoding();

    /// <summary>
    /// An optional progress reporter for tracking compression operations.
    /// When set, progress updates will be reported as entries are written.
    /// </summary>
    public IProgress<ProgressReport>? Progress { get; init; }

    /// <summary>
    /// Creates a new WriterOptions instance with the specified compression type.
    /// Compression level is automatically set based on the compression type.
    /// </summary>
    /// <param name="compressionType">The compression type for the archive.</param>
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

    /// <summary>
    /// Creates a new WriterOptions instance with the specified compression type and level.
    /// </summary>
    /// <param name="compressionType">The compression type for the archive.</param>
    /// <param name="compressionLevel">The compression level (algorithm-specific).</param>
    public WriterOptions(CompressionType compressionType, int compressionLevel)
    {
        CompressionType = compressionType;
        CompressionLevel = compressionLevel;
    }

    /// <summary>
    /// Creates a new WriterOptions instance with the specified compression type and stream open behavior.
    /// </summary>
    /// <param name="compressionType">The compression type for the archive.</param>
    /// <param name="leaveStreamOpen">Whether to leave the stream open after writing.</param>
    public WriterOptions(CompressionType compressionType, bool leaveStreamOpen)
        : this(compressionType)
    {
        LeaveStreamOpen = leaveStreamOpen;
    }

    /// <summary>
    /// Creates a new WriterOptions instance with the specified compression type, level, and stream open behavior.
    /// </summary>
    /// <param name="compressionType">The compression type for the archive.</param>
    /// <param name="compressionLevel">The compression level (algorithm-specific).</param>
    /// <param name="leaveStreamOpen">Whether to leave the stream open after writing.</param>
    public WriterOptions(
        CompressionType compressionType,
        int compressionLevel,
        bool leaveStreamOpen
    )
        : this(compressionType, compressionLevel)
    {
        LeaveStreamOpen = leaveStreamOpen;
    }

    /// <summary>
    /// Implicit conversion from CompressionType to WriterOptions.
    /// </summary>
    /// <param name="compressionType">The compression type.</param>
    public static implicit operator WriterOptions(CompressionType compressionType) =>
        new(compressionType);
}
