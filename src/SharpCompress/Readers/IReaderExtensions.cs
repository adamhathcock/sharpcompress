#if !NO_FILE
using System.IO;
using SharpCompress.Common;
#endif

namespace SharpCompress.Readers
{
    public static class IReaderExtensions
    {
#if !NO_FILE
        public static void WriteEntryTo(this IReader reader, string filePath)
        {
            using (Stream stream = File.Open(filePath, FileMode.Create, FileAccess.Write))
            {
                reader.WriteEntryTo(stream);
            }
        }

        public static void WriteEntryTo(this IReader reader, FileInfo filePath)
        {
            using (Stream stream = filePath.Open(FileMode.Create))
            {
                reader.WriteEntryTo(stream);
            }
        }

        /// <summary>
        /// Extract all remaining unread entries to specific directory, retaining filename
        /// </summary>
        public static void WriteAllToDirectory(this IReader reader, string destinationDirectory,
                                               ExtractionOptions options = null)
        {
            while (reader.MoveToNextEntry())
            {
                reader.WriteEntryToDirectory(destinationDirectory, options);
            }
        }

        /// <summary>
        /// Extract to specific directory, retaining filename
        /// </summary>
        public static void WriteEntryToDirectory(this IReader reader, string destinationDirectory,
                                                 ExtractionOptions options = null)
        {
            string destinationFileName = string.Empty;
            string file = Path.GetFileName(reader.Entry.Key);
            options = options ?? new ExtractionOptions()
                      {
                          Overwrite = true
                      };

            if (options.ExtractFullPath)
            {
                string folder = Path.GetDirectoryName(reader.Entry.Key);
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

            if (!reader.Entry.IsDirectory)
            {
                reader.WriteEntryToFile(destinationFileName, options);
            }
            else if (options.ExtractFullPath && !Directory.Exists(destinationFileName))
            {
                Directory.CreateDirectory(destinationFileName);
            }
        }

        /// <summary>
        /// Extract to specific file
        /// </summary>
        public static void WriteEntryToFile(this IReader reader, string destinationFileName,
                                            ExtractionOptions options = null)
        {
            FileMode fm = FileMode.Create;
            options = options ?? new ExtractionOptions()
            {
                Overwrite = true
            };

            if (!options.Overwrite)
            {
                fm = FileMode.CreateNew;
            }
            using (FileStream fs = File.Open(destinationFileName, fm))
            {
                reader.WriteEntryTo(fs);
                //using (Stream s = reader.OpenEntryStream())
                //{
                //    s.TransferTo(fs);
                //}
            }
            reader.Entry.PreserveExtractionOptions(destinationFileName, options);
        }
#endif
    }
}