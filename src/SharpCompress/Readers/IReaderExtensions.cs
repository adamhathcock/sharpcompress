using System.IO;

namespace SharpCompress.Readers;

/// <summary>Convenience extraction using the default extraction service.</summary>
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

        public void WriteAllToDirectory(
            string destinationDirectory,
            Common.ExtractionOptions? options = null
        ) =>
            ClientDefaults.Client.Extractor.ExtractToDirectory(
                reader,
                destinationDirectory,
                options
            );

        public void WriteEntryToDirectory(
            string destinationDirectory,
            Common.ExtractionOptions? options = null
        ) =>
            ClientDefaults.Client.Extractor.ExtractEntryToDirectory(
                reader,
                destinationDirectory,
                options
            );

        public void WriteEntryToFile(
            string destinationFileName,
            Common.ExtractionOptions? options = null
        ) => ClientDefaults.Client.Extractor.ExtractToFile(reader, destinationFileName, options);
    }
}
