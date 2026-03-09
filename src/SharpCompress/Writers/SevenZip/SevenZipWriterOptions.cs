using System;
using SharpCompress.Common;
using SharpCompress.Common.Options;
using SharpCompress.Compressors.LZMA;
using SharpCompress.Providers;

namespace SharpCompress.Writers.SevenZip;

/// <summary>
/// Options for configuring 7z writer behavior.
/// </summary>
public sealed record SevenZipWriterOptions : IWriterOptions
{
    private CompressionType _compressionType;
    private int _compressionLevel;

    /// <summary>
    /// The compression type to use. Supported: LZMA and LZMA2 (default).
    /// </summary>
    public CompressionType CompressionType
    {
        get => _compressionType;
        init
        {
            if (value != CompressionType.LZMA && value != CompressionType.LZMA2)
            {
                throw new ArgumentException(
                    $"SevenZipWriter only supports CompressionType.LZMA and CompressionType.LZMA2. Got: {value}",
                    nameof(value)
                );
            }
            _compressionType = value;
        }
    }

    /// <summary>
    /// Compression level (not used for LZMA in this implementation; reserved for future use).
    /// </summary>
    public int CompressionLevel
    {
        get => _compressionLevel;
        init => _compressionLevel = value;
    }

    /// <summary>
    /// SharpCompress will keep the supplied streams open. Default is true.
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
    /// Registry of compression providers.
    /// Defaults to <see cref="CompressionProviderRegistry.Default" /> but can be replaced with custom implementations.
    /// </summary>
    public CompressionProviderRegistry Providers { get; init; } =
        CompressionProviderRegistry.Default;

    /// <summary>
    /// Controls whether consecutive file writes are grouped into shared 7z folders.
    /// Default is disabled so each file is compressed independently.
    /// </summary>
    public SevenZipSolidOptions Solid { get; init; } = SevenZipSolidOptions.Disabled;

    /// <summary>
    /// Whether to compress the archive header itself using LZMA.
    /// Default is true, matching standard 7-Zip behavior.
    /// </summary>
    public bool CompressHeader { get; init; } = true;

    /// <summary>
    /// Custom LZMA encoder properties. Null uses defaults (1MB dictionary, 32 fast bytes).
    /// </summary>
    public LzmaEncoderProperties? LzmaProperties { get; init; }

    /// <summary>
    /// Creates a new SevenZipWriterOptions instance with LZMA2 compression (default).
    /// </summary>
    public SevenZipWriterOptions()
    {
        CompressionType = CompressionType.LZMA2;
    }

    /// <summary>
    /// Creates a new SevenZipWriterOptions instance with the specified compression type.
    /// </summary>
    /// <param name="compressionType">The compression type for the archive.</param>
    public SevenZipWriterOptions(CompressionType compressionType)
    {
        CompressionType = compressionType;
    }

    /// <summary>
    /// Creates a new SevenZipWriterOptions instance from an existing WriterOptions instance.
    /// </summary>
    /// <param name="options">The WriterOptions to copy values from.</param>
    public SevenZipWriterOptions(WriterOptions options)
    {
        CompressionType = options.CompressionType;
        CompressionLevel = options.CompressionLevel;
        LeaveStreamOpen = options.LeaveStreamOpen;
        ArchiveEncoding = options.ArchiveEncoding;
        Progress = options.Progress;
        Providers = options.Providers;
    }

    /// <summary>
    /// Creates a new SevenZipWriterOptions from an existing IWriterOptions instance.
    /// </summary>
    /// <param name="options">The IWriterOptions to copy values from.</param>
    public SevenZipWriterOptions(IWriterOptions options)
    {
        CompressionType = options.CompressionType;
        CompressionLevel = options.CompressionLevel;
        LeaveStreamOpen = options.LeaveStreamOpen;
        ArchiveEncoding = options.ArchiveEncoding;
        Progress = options.Progress;
        Providers = options.Providers;

        if (options is SevenZipWriterOptions sevenZipOptions)
        {
            Solid = sevenZipOptions.Solid;
            CompressHeader = sevenZipOptions.CompressHeader;
            LzmaProperties = sevenZipOptions.LzmaProperties;
        }
    }

    /// <summary>
    /// Implicit conversion from CompressionType to SevenZipWriterOptions.
    /// </summary>
    public static implicit operator SevenZipWriterOptions(CompressionType compressionType) =>
        new(compressionType);
}
