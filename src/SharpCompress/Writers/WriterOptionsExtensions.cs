using System;
using SharpCompress.Common;
using SharpCompress.Common.Options;
using SharpCompress.Writers.GZip;
using SharpCompress.Writers.Tar;
using SharpCompress.Writers.Zip;

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
    /// Creates a copy with the specified LeaveStreamOpen value.
    /// Works with any IWriterOptions implementation.
    /// </summary>
    /// <param name="options">The source options.</param>
    /// <param name="leaveStreamOpen">Whether to leave the stream open.</param>
    /// <returns>A new options instance with the specified LeaveStreamOpen value.</returns>
    public static IWriterOptions WithLeaveStreamOpen(
        this IWriterOptions options,
        bool leaveStreamOpen
    ) =>
        options switch
        {
            WriterOptions writerOptions => writerOptions with { LeaveStreamOpen = leaveStreamOpen },
            ZipWriterOptions zipOptions => zipOptions with { LeaveStreamOpen = leaveStreamOpen },
            TarWriterOptions tarOptions => tarOptions with { LeaveStreamOpen = leaveStreamOpen },
            GZipWriterOptions gzipOptions => gzipOptions with { LeaveStreamOpen = leaveStreamOpen },
            _ => throw new NotSupportedException(
                $"Cannot set LeaveStreamOpen on options of type {options.GetType().Name}. "
                    + "Options must be a record type implementing IWriterOptions."
            ),
        };

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
}
