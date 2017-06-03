using System.IO;
using SharpCompress.Common;
#if THREEFIVE
using SharpCompress.Common.Rar.Headers;
#endif

namespace SharpCompress.Archive
{

    public static class IArchiveEntryExtensions
    {
#if !PORTABLE
        /// <summary>
        /// Extract to specific directory, retaining filename
        /// </summary>
        public static void WriteToDirectory(this IArchiveEntry entry, string destinationDirectory,
            ExtractOptions options = ExtractOptions.Overwrite)
        {
            string destinationFileName = string.Empty;
            string file = Path.GetFileName(entry.FilePath);


            if (options.HasFlag(ExtractOptions.ExtractFullPath))
            {

                string folder = Path.GetDirectoryName(entry.FilePath);
                string destdir = Path.Combine(destinationDirectory, folder);
                if (!Directory.Exists(destdir))
                {
                    Directory.CreateDirectory(destdir);
                }
                destinationFileName = Path.Combine(destdir, file);
            }
            else
            {
                destinationFileName = Path.Combine(destinationDirectory, file);
            }
            entry.WriteToFile(destinationFileName, options);
        }

        /// <summary>
        /// Extract to specific file
        /// </summary>
        public static void WriteToFile(this IArchiveEntry entry, string destinationFileName,
            ExtractOptions options = ExtractOptions.Overwrite)
        {
            if (entry.IsDirectory)
            {
                return;
            }
            FileMode fm = FileMode.Create;

            if (!options.HasFlag(ExtractOptions.Overwrite))
            {
                fm = FileMode.CreateNew;
            }
            using (FileStream fs = File.Open(destinationFileName, fm))
//            using (Stream entryStream = entry.OpenEntryStream())
            {
                //entryStream.TransferTo(fs);
               entry.WriteTo(fs);
            }
        }
#endif
    }
}
