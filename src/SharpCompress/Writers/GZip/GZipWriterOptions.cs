using System;
using SharpCompress.Common;
using SharpCompress.Common.Options;
using SharpCompress.Compressors;
using D = SharpCompress.Compressors.Deflate;

namespace SharpCompress.Writers.GZip;

/// <summary>
/// Options for configuring GZip writer behavior.
/// </summary>
/// <remarks>
/// This class is immutable. Use the <c>with</c> expression to create modified copies:
/// <code>
/// var options = new GZipWriterOptions { CompressionLevel = 9 };
/// options = options with { LeaveStreamOpen = false };
/// </code>
/// </remarks>
public sealed record GZipWriterOptions : IWriterOptions
{
    /// <summary>
    /// The compression type (always GZip for this writer).
    /// </summary>
    public CompressionType CompressionType { get; init; } = CompressionType.GZip;

    /// <summary>
    /// The compression level to be used (0-9 for Deflate).
    /// </summary>
    public int CompressionLevel { get; init; } = (int)D.CompressionLevel.Default;

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
    /// Optional registry of compression providers.
    /// If null, the default registry (SharpCompress internal implementations) will be used.
    /// Use this to provide alternative compression implementations, such as
    /// System.IO.Compression for GZip on modern .NET.
    /// </summary>
    public CompressionProviderRegistry? CompressionProviders { get; init; }

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

    /// <summary>
    /// Creates a new GZipWriterOptions instance with the specified stream open behavior.
    /// </summary>
    /// <param name="leaveStreamOpen">Whether to leave the stream open after writing.</param>
    public GZipWriterOptions(bool leaveStreamOpen)
    {
        LeaveStreamOpen = leaveStreamOpen;
    }

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
