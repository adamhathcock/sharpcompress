using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using SharpCompress.Common;

namespace SharpCompress.Archives;

public static class IArchiveEntryExtensions
{
    private const int BufferSize = 81920;

    public static void WriteTo(this IArchiveEntry archiveEntry, Stream streamToWriteTo)
    {
        if (archiveEntry.IsDirectory)
        {
            throw new ExtractionException("Entry is a file directory and cannot be extracted.");
        }

        var progressInfo = archiveEntry.Archive as IArchiveProgressInfo;
        progressInfo?.EnsureEntriesLoaded();

        IProgress<ProgressReport>? progress = progressInfo?.Progress;
        using var entryStream = archiveEntry.OpenEntryStream();

        if (progress is null)
        {
            entryStream.CopyTo(streamToWriteTo);
        }
        else
        {
            var entryPath = archiveEntry.Key ?? string.Empty;
            long? totalBytes = GetEntrySizeSafe(archiveEntry);
            long transferred = 0;

            var buffer = new byte[BufferSize];
            int bytesRead;
            while ((bytesRead = entryStream.Read(buffer, 0, buffer.Length)) > 0)
            {
                streamToWriteTo.Write(buffer, 0, bytesRead);
                transferred += bytesRead;
                progress.Report(new ProgressReport(entryPath, transferred, totalBytes));
            }
        }
    }

    public static async Task WriteToAsync(
        this IArchiveEntry archiveEntry,
        Stream streamToWriteTo,
        CancellationToken cancellationToken = default
    )
    {
        if (archiveEntry.IsDirectory)
        {
            throw new ExtractionException("Entry is a file directory and cannot be extracted.");
        }

        var progressInfo = archiveEntry.Archive as IArchiveProgressInfo;
        progressInfo?.EnsureEntriesLoaded();

        IProgress<ProgressReport>? progress = progressInfo?.Progress;
        using var entryStream = archiveEntry.OpenEntryStream();

        if (progress is null)
        {
            await entryStream
                .CopyToAsync(streamToWriteTo, BufferSize, cancellationToken)
                .ConfigureAwait(false);
        }
        else
        {
            var entryPath = archiveEntry.Key ?? string.Empty;
            long? totalBytes = GetEntrySizeSafe(archiveEntry);
            long transferred = 0;

            var buffer = new byte[BufferSize];
            int bytesRead;
            while (
                (
                    bytesRead = await entryStream
                        .ReadAsync(buffer, 0, buffer.Length, cancellationToken)
                        .ConfigureAwait(false)
                ) > 0
            )
            {
                await streamToWriteTo
                    .WriteAsync(buffer, 0, bytesRead, cancellationToken)
                    .ConfigureAwait(false);
                transferred += bytesRead;
                progress.Report(new ProgressReport(entryPath, transferred, totalBytes));
            }
        }
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
                await entry.WriteToAsync(fs, cancellationToken).ConfigureAwait(false);
            },
            cancellationToken
        );
}
