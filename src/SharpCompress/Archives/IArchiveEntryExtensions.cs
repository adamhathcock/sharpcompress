using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using SharpCompress.Common;
using SharpCompress.IO;

namespace SharpCompress.Archives;

public static class IArchiveEntryExtensions
{
    private const int BufferSize = 81920;

    /// <summary>
    /// Extract entry to the specified stream.
    /// </summary>
    /// <param name="archiveEntry">The archive entry to extract.</param>
    /// <param name="streamToWriteTo">The stream to write the entry content to.</param>
    /// <param name="progress">Optional progress reporter for tracking extraction progress.</param>
    public static void WriteTo(
        this IArchiveEntry archiveEntry,
        Stream streamToWriteTo,
        IProgress<ProgressReport>? progress = null
    )
    {
        if (archiveEntry.IsDirectory)
        {
            throw new ExtractionException("Entry is a file directory and cannot be extracted.");
        }

        using var entryStream = archiveEntry.OpenEntryStream();
        var sourceStream = WrapWithProgress(entryStream, archiveEntry, progress);
        sourceStream.CopyTo(streamToWriteTo, BufferSize);
    }

    /// <summary>
    /// Extract entry to the specified stream asynchronously.
    /// </summary>
    /// <param name="archiveEntry">The archive entry to extract.</param>
    /// <param name="streamToWriteTo">The stream to write the entry content to.</param>
    /// <param name="progress">Optional progress reporter for tracking extraction progress.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public static async Task WriteToAsync(
        this IArchiveEntry archiveEntry,
        Stream streamToWriteTo,
        IProgress<ProgressReport>? progress = null,
        CancellationToken cancellationToken = default
    )
    {
        if (archiveEntry.IsDirectory)
        {
            throw new ExtractionException("Entry is a file directory and cannot be extracted.");
        }

        using var entryStream = archiveEntry.OpenEntryStream();
        var sourceStream = WrapWithProgress(entryStream, archiveEntry, progress);
        await sourceStream
            .CopyToAsync(streamToWriteTo, BufferSize, cancellationToken)
            .ConfigureAwait(false);
    }

    private static Stream WrapWithProgress(
        Stream source,
        IArchiveEntry entry,
        IProgress<ProgressReport>? progress
    )
    {
        if (progress is null)
        {
            return source;
        }

        var entryPath = entry.Key ?? string.Empty;
        long? totalBytes = GetEntrySizeSafe(entry);
        return new ProgressReportingStream(
            source,
            progress,
            entryPath,
            totalBytes,
            leaveOpen: true
        );
    }

    private static long? GetEntrySizeSafe(IArchiveEntry entry)
    {
        try
        {
            var size = entry.Size;
            return size >= 0 ? size : null;
        }
        catch (NotImplementedException)
        {
            return null;
        }
    }

    /// <summary>
    /// Extract to specific directory, retaining filename
    /// </summary>
    public static void WriteToDirectory(
        this IArchiveEntry entry,
        string destinationDirectory,
        ExtractionOptions? options = null
    ) =>
        ExtractionMethods.WriteEntryToDirectory(
            entry,
            destinationDirectory,
            options,
            entry.WriteToFile
        );

    /// <summary>
    /// Extract to specific directory asynchronously, retaining filename
    /// </summary>
    public static Task WriteToDirectoryAsync(
        this IArchiveEntry entry,
        string destinationDirectory,
        ExtractionOptions? options = null,
        CancellationToken cancellationToken = default
    ) =>
        ExtractionMethods.WriteEntryToDirectoryAsync(
            entry,
            destinationDirectory,
            options,
            (x, opt) => entry.WriteToFileAsync(x, opt, cancellationToken),
            cancellationToken
        );

    /// <summary>
    /// Extract to specific file
    /// </summary>
    public static void WriteToFile(
        this IArchiveEntry entry,
        string destinationFileName,
        ExtractionOptions? options = null
    ) =>
        ExtractionMethods.WriteEntryToFile(
            entry,
            destinationFileName,
            options,
            (x, fm) =>
            {
                using var fs = File.Open(destinationFileName, fm);
                entry.WriteTo(fs);
            }
        );

    /// <summary>
    /// Extract to specific file asynchronously
    /// </summary>
    public static Task WriteToFileAsync(
        this IArchiveEntry entry,
        string destinationFileName,
        ExtractionOptions? options = null,
        CancellationToken cancellationToken = default
    ) =>
        ExtractionMethods.WriteEntryToFileAsync(
            entry,
            destinationFileName,
            options,
            async (x, fm) =>
            {
                using var fs = File.Open(destinationFileName, fm);
                await entry
                    .WriteToAsync(fs, progress: null, cancellationToken: cancellationToken)
                    .ConfigureAwait(false);
            },
            cancellationToken
        );
}
