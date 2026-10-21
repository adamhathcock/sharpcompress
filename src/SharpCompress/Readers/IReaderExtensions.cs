using System.IO;
using SharpCompress.Common;
using SharpCompress.Services;

namespace SharpCompress.Readers;

public static class IReaderExtensions
{
    extension(IReader reader)
    {
        public void WriteEntryTo(string filePath) =>
            LibraryDefaults.Composition.ReaderExtractor.WriteEntryTo(reader, filePath);

        public void WriteEntryTo(FileInfo filePath) =>
            LibraryDefaults.Composition.ReaderExtractor.WriteEntryTo(reader, filePath);

        /// <summary>Extract all remaining unread entries to specific directory, retaining filename.</summary>
        public void WriteAllToDirectory(
            string destinationDirectory,
            ExtractionOptions? options = null
        ) =>
            LibraryDefaults.Composition.ReaderExtractor.WriteAllToDirectory(
                reader,
                destinationDirectory,
                options
            );

        /// <summary>Extract to specific directory, retaining filename.</summary>
        public void WriteEntryToDirectory(
            string destinationDirectory,
            ExtractionOptions? options = null
        ) =>
            LibraryDefaults.Composition.ReaderExtractor.WriteEntryToDirectory(
                reader,
                destinationDirectory,
                options
            );

        /// <summary>Extract to specific file.</summary>
        public void WriteEntryToFile(
            string destinationFileName,
            ExtractionOptions? options = null
        ) =>
            LibraryDefaults.Composition.ReaderExtractor.WriteEntryToFile(
                reader,
                destinationFileName,
                options
            );
    }
}
