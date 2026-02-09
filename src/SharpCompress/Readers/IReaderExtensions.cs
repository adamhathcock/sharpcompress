using System.IO;
using SharpCompress.Common;

namespace SharpCompress.Readers;

public static class IReaderExtensions
{
    extension(IReader reader)
    {
        public void WriteEntryTo(string filePath)
        {
            using Stream stream = File.Open(filePath, FileMode.Create, FileAccess.Write);
            reader.WriteEntryTo(stream);
        }

        public void WriteEntryTo(FileInfo filePath)
        {
            using Stream stream = filePath.Open(FileMode.Create);
            reader.WriteEntryTo(stream);
        }

        /// <summary>
        /// Extract all remaining unread entries to specific directory, retaining filename
        /// </summary>
        public void WriteAllToDirectory(string destinationDirectory)
        {
            while (reader.MoveToNextEntry())
            {
                reader.WriteEntryToDirectory(destinationDirectory);
            }
        }

        /// <summary>
        /// Extract to specific directory, retaining filename
        /// </summary>
        public void WriteEntryToDirectory(string destinationDirectory) =>
            ExtractionMethods.WriteEntryToDirectory(
                reader.Entry,
                destinationDirectory,
                (path) => reader.WriteEntryToFile(path)
            );

        /// <summary>
        /// Extract to specific file
        /// </summary>
        public void WriteEntryToFile(string destinationFileName) =>
            ExtractionMethods.WriteEntryToFile(
                reader.Entry,
                destinationFileName,
                (x, fm) =>
                {
                    using var fs = File.Open(destinationFileName, fm);
                    reader.WriteEntryTo(fs);
                }
            );
    }
}
