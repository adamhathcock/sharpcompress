using System;
using SharpCompress.Common;
using SharpCompress.Common.Options;
using SharpCompress.Compressors;
using D = SharpCompress.Compressors.Deflate;

namespace SharpCompress.Writers;

/// <summary>
/// Options for configuring writer behavior when creating archives.
/// </summary>
/// <remarks>
/// This class is immutable. Use factory methods for creation:
/// <code>
/// var options = WriterOptions.ForZip().WithLeaveStreamOpen(false).WithCompressionLevel(9);
/// </code>
/// </remarks>
public sealed record WriterOptions : IWriterOptions
{
    private CompressionType _compressionType;
    private int _compressionLevel;

    /// <summary>
    /// The compression type to use for the archive.
    /// </summary>
    public CompressionType CompressionType
    {
        get => _compressionType;
        init => _compressionType = value;
    }

    /// <summary>
    /// The compression level to be used when the compression type supports variable levels.
    /// Valid ranges depend on the compression algorithm:
    /// - Deflate/GZip: 0-9 (0=no compression, 6=default, 9=best compression)
    /// - ZStandard: 1-22 (1=fastest, 3=default, 22=best compression)
    /// Note: BZip2 and LZMA do not support compression levels in this implementation.
    /// Defaults are set automatically based on compression type in the constructor.
    /// </summary>
    public int CompressionLevel
    {
        get => _compressionLevel;
        init
        {
            CompressionLevelValidation.Validate(CompressionType, value);
            _compressionLevel = value;
        }
    }

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
    /// Optional registry of compression providers.
    /// If null, the default registry (SharpCompress internal implementations) will be used.
    /// Use this to provide alternative compression implementations, such as
    /// System.IO.Compression for Deflate/GZip on modern .NET.
    /// </summary>
    public CompressionProviderRegistry? CompressionProviders { get; init; }

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

    // Note: Constructors with boolean leaveStreamOpen parameter removed.
    // Use the fluent WithLeaveStreamOpen() helper or object initializer instead:
    // new WriterOptions(type) { LeaveStreamOpen = false }
    // or
    // WriterOptions.ForZip().WithLeaveStreamOpen(false)

    /// <summary>
    /// Implicit conversion from CompressionType to WriterOptions.
    /// </summary>
    /// <param name="compressionType">The compression type.</param>
    public static implicit operator WriterOptions(CompressionType compressionType) =>
        new(compressionType);

    /// <summary>
    /// Creates a new ZipWriterOptions for writing ZIP archives.
    /// </summary>
    /// <param name="compressionType">The compression type for the archive. Defaults to Deflate.</param>
    public static WriterOptions ForZip(CompressionType compressionType = CompressionType.Deflate) =>
        new(compressionType);

    /// <summary>
    /// Creates a new WriterOptions for writing TAR archives.
    /// </summary>
    /// <param name="compressionType">The compression type for the archive. Defaults to None.</param>
    public static WriterOptions ForTar(CompressionType compressionType = CompressionType.None) =>
        new(compressionType);

    /// <summary>
    /// Creates a new WriterOptions for writing GZip compressed files.
    /// </summary>
    public static WriterOptions ForGZip() => new(CompressionType.GZip);
}
