using System.IO;
using System.Threading;
using System.Threading.Tasks;
using SharpCompress.Common;
using SharpCompress.IO;

namespace SharpCompress.Archives;

public static class IArchiveEntryExtensions
{
    public static void WriteTo(this IArchiveEntry archiveEntry, Stream streamToWriteTo)
    {
        if (archiveEntry.IsDirectory)
        {
            throw new ExtractionException("Entry is a file directory and cannot be extracted.");
        }

        var streamListener = (IArchiveExtractionListener)archiveEntry.Archive;
        streamListener.EnsureEntriesLoaded();
        streamListener.FireEntryExtractionBegin(archiveEntry);
        streamListener.FireFilePartExtractionBegin(
            archiveEntry.Key ?? "Key",
            archiveEntry.Size,
            archiveEntry.CompressedSize
        );
        var entryStream = archiveEntry.OpenEntryStream();
        using (entryStream)
        {
            using Stream s = new ListeningStream(streamListener, entryStream);
            s.CopyTo(streamToWriteTo);
        }
        streamListener.FireEntryExtractionEnd(archiveEntry);
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

        var streamListener = (IArchiveExtractionListener)archiveEntry.Archive;
        streamListener.EnsureEntriesLoaded();
        streamListener.FireEntryExtractionBegin(archiveEntry);
        streamListener.FireFilePartExtractionBegin(
            archiveEntry.Key ?? "Key",
            archiveEntry.Size,
            archiveEntry.CompressedSize
        );
        var entryStream = archiveEntry.OpenEntryStream();
        using (entryStream)
        {
            using Stream s = new ListeningStream(streamListener, entryStream);
            await s.CopyToAsync(streamToWriteTo, 81920, cancellationToken).ConfigureAwait(false);
        }
        streamListener.FireEntryExtractionEnd(archiveEntry);
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
            }
        );
}
