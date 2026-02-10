using System;
using SharpCompress.Common;
using SharpCompress.Common.Options;
using SharpCompress.Common.Tar.Headers;

namespace SharpCompress.Writers.Tar;

/// <summary>
/// Options for configuring Tar writer behavior.
/// </summary>
/// <remarks>
/// This class is immutable. Use the <c>with</c> expression to create modified copies:
/// <code>
/// var options = new TarWriterOptions(CompressionType.GZip, true);
/// options = options with { HeaderFormat = TarHeaderWriteFormat.V7 };
/// </code>
/// </remarks>
public sealed record TarWriterOptions : IWriterOptions
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
    /// Indicates if archive should be finalized (by 2 empty blocks) on close.
    /// </summary>
    public bool FinalizeArchiveOnClose { get; init; } = true;

    /// <summary>
    /// The format to use when writing tar headers.
    /// </summary>
    public TarHeaderWriteFormat HeaderFormat { get; init; } =
        TarHeaderWriteFormat.GNU_TAR_LONG_LINK;

    /// <summary>
    /// Creates a new TarWriterOptions instance with the specified compression type and finalization option.
    /// </summary>
    /// <param name="compressionType">The compression type for the archive.</param>
    /// <param name="finalizeArchiveOnClose">Whether to finalize the archive on close.</param>
    public TarWriterOptions(CompressionType compressionType, bool finalizeArchiveOnClose)
    {
        CompressionType = compressionType;
        FinalizeArchiveOnClose = finalizeArchiveOnClose;
        CompressionLevel = compressionType switch
        {
            CompressionType.ZStandard => 3,
            _ => 0,
        };
    }

    /// <summary>
    /// Creates a new TarWriterOptions instance with the specified compression type, finalization option, and header format.
    /// </summary>
    /// <param name="compressionType">The compression type for the archive.</param>
    /// <param name="finalizeArchiveOnClose">Whether to finalize the archive on close.</param>
    /// <param name="headerFormat">The tar header format.</param>
    public TarWriterOptions(
        CompressionType compressionType,
        bool finalizeArchiveOnClose,
        TarHeaderWriteFormat headerFormat
    )
        : this(compressionType, finalizeArchiveOnClose)
    {
        HeaderFormat = headerFormat;
    }

    /// <summary>
    /// Creates a new TarWriterOptions instance from an existing WriterOptions instance.
    /// </summary>
    /// <param name="options">The WriterOptions to copy values from.</param>
    public TarWriterOptions(WriterOptions options)
    {
        CompressionType = options.CompressionType;
        CompressionLevel = options.CompressionLevel;
        LeaveStreamOpen = options.LeaveStreamOpen;
        ArchiveEncoding = options.ArchiveEncoding;
        Progress = options.Progress;
    }

    /// <summary>
    /// Creates a new TarWriterOptions instance from an existing IWriterOptions instance.
    /// </summary>
    /// <param name="options">The IWriterOptions to copy values from.</param>
    public TarWriterOptions(IWriterOptions options)
    {
        CompressionType = options.CompressionType;
        CompressionLevel = options.CompressionLevel;
        LeaveStreamOpen = options.LeaveStreamOpen;
        ArchiveEncoding = options.ArchiveEncoding;
        Progress = options.Progress;
    }

    /// <summary>
    /// Implicit conversion from CompressionType to TarWriterOptions with finalize enabled.
    /// </summary>
    /// <param name="compressionType">The compression type.</param>
    public static implicit operator TarWriterOptions(CompressionType compressionType) =>
        new(compressionType, true);
}
