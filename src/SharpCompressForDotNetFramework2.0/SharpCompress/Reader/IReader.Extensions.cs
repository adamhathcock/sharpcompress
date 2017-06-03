﻿using System.IO;
using SharpCompress.Common;
#if THREEFIVE
using SharpCompress.Common.Rar.Headers;
#endif

namespace SharpCompress.Reader
{
    public static class IReaderExtensions
    {
#if !PORTABLE
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
            ExtractOptions options = ExtractOptions.Overwrite)
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
            ExtractOptions options = ExtractOptions.Overwrite)
        {
            string destinationFileName = string.Empty;
            string file = Path.GetFileName(reader.Entry.FilePath);


            if (options.HasFlag(ExtractOptions.ExtractFullPath))
            {
                string folder = Path.GetDirectoryName(reader.Entry.FilePath);
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
            else if (options.HasFlag(ExtractOptions.ExtractFullPath) && !Directory.Exists(destinationFileName))
            {
                Directory.CreateDirectory(destinationFileName);
            }
        }

        /// <summary>
        /// Extract to specific file
        /// </summary>
        public static void WriteEntryToFile(this IReader reader, string destinationFileName,
            ExtractOptions options = ExtractOptions.Overwrite)
        {
            FileMode fm = FileMode.Create;

            if (!options.HasFlag(ExtractOptions.Overwrite))
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
        }
#endif
    }
}
