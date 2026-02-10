using System;
using SharpCompress.Common;
using SharpCompress.Common.Options;
using SharpCompress.Writers;
using D = SharpCompress.Compressors.Deflate;

namespace SharpCompress.Writers.GZip;

/// <summary>
/// Options for configuring GZip writer behavior.
/// </summary>
/// <remarks>
/// This class is immutable. Use factory methods for creation:
/// <code>
/// var options = WriterOptions.ForGZip().WithLeaveStreamOpen(false).WithCompressionLevel(9);
/// </code>
/// </remarks>
public sealed record GZipWriterOptions : IWriterOptions
{
    private int _compressionLevel = (int)D.CompressionLevel.Default;

    /// <summary>
    /// The compression type (always GZip for this writer).
    /// </summary>
    public CompressionType CompressionType
    {
        get => CompressionType.GZip;
        init
        {
            if (value != CompressionType.GZip)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(CompressionType),
                    value,
                    "GZipWriterOptions only supports CompressionType.GZip."
                );
            }
        }
    }

    /// <summary>
    /// The compression level to be used (0-9 for Deflate).
    /// </summary>
    public int CompressionLevel
    {
        get => _compressionLevel;
        init
        {
            CompressionLevelValidation.Validate(CompressionType.GZip, value);
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
    /// </summary>
    public IProgress<ProgressReport>? Progress { get; init; }

    /// <summary>
    /// Creates a new GZipWriterOptions instance with default values.
    /// </summary>
    public GZipWriterOptions() { }

    /// <summary>
    /// Creates a new GZipWriterOptions instance with the specified compression level.
    /// </summary>
    /// <param name="compressionLevel">The compression level (0-9).</param>
    public GZipWriterOptions(int compressionLevel)
    {
        CompressionLevel = compressionLevel;
    }

    /// <summary>
    /// Creates a new GZipWriterOptions instance with the specified Deflate compression level.
    /// </summary>
    /// <param name="compressionLevel">The Deflate compression level.</param>
    public GZipWriterOptions(D.CompressionLevel compressionLevel)
    {
        CompressionLevel = (int)compressionLevel;
    }

    // Note: Constructor with boolean leaveStreamOpen parameter removed.
    // Use the fluent WithLeaveStreamOpen() helper or object initializer instead:
    // new GZipWriterOptions() { LeaveStreamOpen = false }
    // or
    // WriterOptions.ForGZip().WithLeaveStreamOpen(false)

    /// <summary>
    /// Creates a new GZipWriterOptions instance from an existing WriterOptions instance.
    /// </summary>
    /// <param name="options">The WriterOptions to copy values from.</param>
    public GZipWriterOptions(WriterOptions options)
    {
        CompressionLevel = options.CompressionLevel;
        LeaveStreamOpen = options.LeaveStreamOpen;
        ArchiveEncoding = options.ArchiveEncoding;
        Progress = options.Progress;
    }

    /// <summary>
    /// Creates a new GZipWriterOptions instance from an existing IWriterOptions instance.
    /// </summary>
    /// <param name="options">The IWriterOptions to copy values from.</param>
    public GZipWriterOptions(IWriterOptions options)
    {
        CompressionLevel = options.CompressionLevel;
        LeaveStreamOpen = options.LeaveStreamOpen;
        ArchiveEncoding = options.ArchiveEncoding;
        Progress = options.Progress;
    }
}
