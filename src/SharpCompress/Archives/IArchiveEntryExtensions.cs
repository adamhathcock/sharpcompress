using System.IO;
using System.Threading.Tasks;
using SharpCompress.Common;
using SharpCompress.IO;

namespace SharpCompress.Archives;

public static class IArchiveEntryExtensions
{
    public static async Task WriteToAsync(this IArchiveEntry archiveEntry, Stream streamToWriteTo)
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
        var entryStream = await archiveEntry.OpenEntryStreamAsync();
        using (entryStream)
        {
            using Stream s = new ListeningStream(streamListener, entryStream);
            await s.TransferToAsync(streamToWriteTo);
        }
        streamListener.FireEntryExtractionEnd(archiveEntry);
    }

    /// <summary>
    /// Extract to specific directory, retaining filename
    /// </summary>
    public static async Task WriteEntryToDirectoryAsync(
        this IArchiveEntry entry,
        string destinationDirectory,
        ExtractionOptions? options = null
    ) =>
        await ExtractionMethods.WriteEntryToDirectoryAsync(
            entry,
            destinationDirectory,
            options,
            entry.WriteToFileAsync
        );

    /// <summary>
    /// Extract to specific file
    /// </summary>
    public static Task WriteToFileAsync(
        this IArchiveEntry entry,
        string destinationFileName,
        ExtractionOptions? options = null
    ) =>
        ExtractionMethods.WriteEntryToFileAsync(
            entry,
            destinationFileName,
            options,
            async (x, fm) =>
            {
                using var fs = File.Open(destinationFileName, fm);
              await  entry.WriteToAsync(fs);
            }
        );
}
