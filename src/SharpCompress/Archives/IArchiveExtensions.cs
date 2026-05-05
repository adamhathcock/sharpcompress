using System;
using SharpCompress.Common;
using SharpCompress.Readers;

namespace SharpCompress.Archives;

public static class IArchiveExtensions
{
    extension(IArchive archive)
    {
        /// <summary>
        /// Extract to specific directory with progress reporting
        /// </summary>
        /// <param name="destinationDirectory">The folder to extract into.</param>
        /// <param name="options">Extraction options.</param>
        /// <param name="progress">Optional progress reporter for tracking extraction progress.</param>
        public void WriteToDirectory(
            string destinationDirectory,
            ExtractionOptions? options = null,
            IProgress<ProgressReport>? progress = null
        )
        {
            if (archive.IsSolid || archive.Type == ArchiveType.SevenZip)
            {
                using var reader = archive.ExtractAllEntries();
                reader.WriteAllToDirectory(destinationDirectory, options);
            }
            else
            {
                archive.WriteToDirectoryInternal(destinationDirectory, options, progress);
            }
        }

        private void WriteToDirectoryInternal(
            string destinationDirectory,
            ExtractionOptions? options,
            IProgress<ProgressReport>? progress
        )
        {
            options ??= new ExtractionOptions();
            var fullDestinationDirectoryPath = DirectoryManagement.GetFullDestinationDirectoryPath(
                destinationDirectory
            );

            var totalBytes = archive.TotalUncompressedSize;
            var bytesRead = 0L;

            foreach (var entry in archive.Entries)
            {
                if (entry.IsDirectory)
                {
                    ExtractionMethods.WriteEntryToDirectoryCore(
                        entry,
                        fullDestinationDirectoryPath,
                        options,
                        _ => { }
                    );
                    continue;
                }

                ExtractionMethods.WriteEntryToDirectoryCore(
                    entry,
                    fullDestinationDirectoryPath,
                    options,
                    path => entry.WriteToFile(path, options)
                );

                bytesRead += entry.Size;
                progress?.Report(
                    new ProgressReport(entry.Key ?? string.Empty, bytesRead, totalBytes)
                );
            }
        }
    }
}
