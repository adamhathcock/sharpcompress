using System.Linq;
using SharpCompress.Common;

namespace SharpCompress.Archive
{
    public static class IArchiveExtensions
    {
#if !NO_FILE
        /// <summary>
        /// Extract to specific directory, retaining filename
        /// </summary>
        public static void WriteToDirectory(this IArchive archive, string destinationDirectory,
                                            ExtractOptions options = ExtractOptions.Overwrite)
        {
            foreach (IArchiveEntry entry in archive.Entries.Where(x => !x.IsDirectory))
            {
                entry.WriteToDirectory(destinationDirectory, options);
            }
        }
#endif
    }
}