using System;
using SharpCompress.Common;
using SharpCompress.Common.Options;
using SharpCompress.Compressors.Deflate;
using D = SharpCompress.Compressors.Deflate;

namespace SharpCompress.Writers.Zip;

/// <summary>
/// Options for configuring Zip writer behavior.
/// </summary>
/// <remarks>
/// This class is immutable. Use the <c>with</c> expression to create modified copies:
/// <code>
/// var options = new ZipWriterOptions(CompressionType.Zip);
/// options = options with { UseZip64 = true };
/// </code>
/// </remarks>
public sealed record ZipWriterOptions : IWriterOptions
{
    /// <summary>
    /// The compression type to use for the archive.
    /// </summary>
    public CompressionType CompressionType { get; init; }

    /// <summary>
    /// The compression level to be used when the compression type supports variable levels.
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
    /// </summary>
    public IProgress<ProgressReport>? Progress { get; init; }

    /// <summary>
    /// Optional comment for the archive.
    /// </summary>
    public string? ArchiveComment { get; init; }

    /// <summary>
    /// Sets a value indicating if zip64 support is enabled.
    /// If this is not set, individual stream lengths cannot exceed 4 GiB.
    /// This option is not supported for non-seekable streams.
    /// Archives larger than 4GiB are supported as long as all streams
    /// are less than 4GiB in length.
    /// </summary>
    public bool UseZip64 { get; init; }

    /// <summary>
    /// Creates a new ZipWriterOptions instance with the specified compression type.
    /// </summary>
    /// <param name="compressionType">The compression type for the archive.</param>
    public ZipWriterOptions(CompressionType compressionType)
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
    /// Creates a new ZipWriterOptions instance with the specified compression type and level.
    /// </summary>
    /// <param name="compressionType">The compression type for the archive.</param>
    /// <param name="compressionLevel">The compression level (algorithm-specific).</param>
    public ZipWriterOptions(CompressionType compressionType, int compressionLevel)
    {
        CompressionType = compressionType;
        CompressionLevel = compressionLevel;
    }

    /// <summary>
    /// Creates a new ZipWriterOptions instance with the specified compression type and Deflate compression level.
    /// </summary>
    /// <param name="compressionType">The compression type for the archive.</param>
    /// <param name="compressionLevel">The Deflate compression level.</param>
    public ZipWriterOptions(CompressionType compressionType, D.CompressionLevel compressionLevel)
        : this(compressionType, (int)compressionLevel) { }

    /// <summary>
    /// Creates a new ZipWriterOptions instance from an existing WriterOptions instance.
    /// </summary>
    /// <param name="options">The WriterOptions to copy values from.</param>
    public ZipWriterOptions(WriterOptions options)
        : this(options.CompressionType, options.CompressionLevel)
    {
        LeaveStreamOpen = options.LeaveStreamOpen;
        ArchiveEncoding = options.ArchiveEncoding;
        Progress = options.Progress;
    }

    /// <summary>
    /// Creates a new ZipWriterOptions instance from an existing IWriterOptions instance.
    /// </summary>
    /// <param name="options">The IWriterOptions to copy values from.</param>
    public ZipWriterOptions(IWriterOptions options)
        : this(options.CompressionType, options.CompressionLevel)
    {
        LeaveStreamOpen = options.LeaveStreamOpen;
        ArchiveEncoding = options.ArchiveEncoding;
        Progress = options.Progress;
    }

    /// <summary>
    /// Implicit conversion from CompressionType to ZipWriterOptions.
    /// </summary>
    /// <param name="compressionType">The compression type.</param>
    public static implicit operator ZipWriterOptions(CompressionType compressionType) =>
        new(compressionType);
}
