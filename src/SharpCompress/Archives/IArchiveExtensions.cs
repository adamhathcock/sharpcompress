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
            foreach (IArchiveEntry entry in archive.Entries.Where(x => !x.IsDirectory))
            {
                await entry.WriteEntryToDirectoryAsync(destinationDirectory, options, cancellationToken);
            }
        }
    }
}