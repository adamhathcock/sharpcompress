using System;
using SharpCompress.Common;
using SharpCompress.Common.Options;
using SharpCompress.Compressors;
using SharpCompress.Providers;

namespace SharpCompress.Writers;

/// <summary>
/// Extension methods for fluent configuration of writer options.
/// </summary>
public static class WriterOptionsExtensions
{
    /// <summary>
    /// Creates a copy with the specified LeaveStreamOpen value.
    /// </summary>
    /// <param name="options">The source options.</param>
    /// <param name="leaveStreamOpen">Whether to leave the stream open.</param>
    /// <returns>A new options instance with the specified LeaveStreamOpen value.</returns>
    public static WriterOptions WithLeaveStreamOpen(
        this WriterOptions options,
        bool leaveStreamOpen
    ) => options with { LeaveStreamOpen = leaveStreamOpen };

    /// <summary>
    /// Creates a copy with the specified compression level.
    /// </summary>
    /// <param name="options">The source options.</param>
    /// <param name="compressionLevel">The compression level (algorithm-specific).</param>
    /// <returns>A new options instance with the specified compression level.</returns>
    public static WriterOptions WithCompressionLevel(
        this WriterOptions options,
        int compressionLevel
    ) => options with { CompressionLevel = compressionLevel };

    /// <summary>
    /// Creates a copy with the specified archive encoding.
    /// </summary>
    /// <param name="options">The source options.</param>
    /// <param name="archiveEncoding">The archive encoding to use.</param>
    /// <returns>A new options instance with the specified archive encoding.</returns>
    public static WriterOptions WithArchiveEncoding(
        this WriterOptions options,
        IArchiveEncoding archiveEncoding
    ) => options with { ArchiveEncoding = archiveEncoding };

    /// <summary>
    /// Creates a copy with the specified progress reporter.
    /// </summary>
    /// <param name="options">The source options.</param>
    /// <param name="progress">The progress reporter.</param>
    /// <returns>A new options instance with the specified progress reporter.</returns>
    public static WriterOptions WithProgress(
        this WriterOptions options,
        IProgress<ProgressReport> progress
    ) => options with { Progress = progress };

    /// <summary>
    /// Creates a copy with the specified compression provider registry.
    /// </summary>
    /// <exception cref="ArgumentNullException">Thrown if <paramref name="providers"/> is null.</exception>
    public static WriterOptions WithProviders(
        this WriterOptions options,
        CompressionProviderRegistry providers
    )
    {
        _ = providers ?? throw new ArgumentNullException(nameof(providers));
        return options with { Providers = providers };
    }
}
