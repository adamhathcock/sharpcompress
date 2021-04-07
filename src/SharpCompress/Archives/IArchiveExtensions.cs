using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using SharpCompress.Common;

namespace SharpCompress.Archives
{
    public static class IArchiveExtensions
    {
        /// <summary>
        /// Extract to specific directory, retaining filename
        /// </summary>
        public static async ValueTask WriteToDirectoryAsync(this IArchive archive, 
                                            string destinationDirectory,
                                            ExtractionOptions? options = null,
                                            CancellationToken cancellationToken = default)
        {
            await foreach (IArchiveEntry entry in archive.Entries.Where(x => !x.IsDirectory).WithCancellation(cancellationToken))
            {
                await entry.WriteEntryToDirectoryAsync(destinationDirectory, options, cancellationToken);
            }
        }
    }
}