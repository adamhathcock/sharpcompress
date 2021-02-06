using System.IO;
using System.Threading;
using System.Threading.Tasks;
using SharpCompress.Common;
using SharpCompress.IO;

namespace SharpCompress.Archives
{
    public static class IArchiveEntryExtensions
    {
        public static async ValueTask WriteToAsync(this IArchiveEntry archiveEntry, Stream streamToWriteTo, CancellationToken cancellationToken = default)
        {
            if (archiveEntry.IsDirectory)
            {
                throw new ExtractionException("Entry is a file directory and cannot be extracted.");
            }

            var streamListener = (IArchiveExtractionListener)archiveEntry.Archive;
            streamListener.EnsureEntriesLoaded();
            streamListener.FireEntryExtractionBegin(archiveEntry);
            streamListener.FireFilePartExtractionBegin(archiveEntry.Key, archiveEntry.Size, archiveEntry.CompressedSize);
            var entryStream = archiveEntry.OpenEntryStream();
            if (entryStream is null)
            {
                return;
            }
            await using (entryStream)
            {
                await using (Stream s = new ListeningStream(streamListener, entryStream))
                {
                    await s.TransferToAsync(streamToWriteTo, cancellationToken);
                }
            }
            streamListener.FireEntryExtractionEnd(archiveEntry);
        }

        /// <summary>
        /// Extract to specific directory, retaining filename
        /// </summary>
        public static ValueTask WriteEntryToDirectoryAsync(this IArchiveEntry entry, 
                                                           string destinationDirectory,
                                                           ExtractionOptions? options = null, 
                                                           CancellationToken cancellationToken = default)
        {
            return ExtractionMethods.WriteEntryToDirectoryAsync(entry, destinationDirectory, options,
                                              entry.WriteToFileAsync, cancellationToken);
        }

        /// <summary>
        /// Extract to specific file
        /// </summary>
        public static ValueTask WriteToFileAsync(this IArchiveEntry entry,
                                       string destinationFileName,
                                       ExtractionOptions? options = null, 
                                       CancellationToken cancellationToken = default)
        {

            return ExtractionMethods.WriteEntryToFileAsync(entry, destinationFileName, options,
                                               async (x, fm, ct) =>
                                               {
                                                   await using FileStream fs = File.Open(x, fm);
                                                   await entry.WriteToAsync(fs, ct);
                                               }, cancellationToken);
        }
    }
}