using System;
using System.Collections.Generic;
using System.IO;
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
        /// <param name="progress">Optional progress reporter for tracking extraction progress.</param>
        public void WriteToDirectory(
            string destinationDirectory,
            IProgress<ProgressReport>? progress = null
        )
        {
            if (archive.IsSolid || archive.Type == ArchiveType.SevenZip)
            {
                using var reader = archive.ExtractAllEntries();
                reader.WriteAllToDirectory(destinationDirectory);
            }
            else
            {
                archive.WriteToDirectoryInternal(destinationDirectory, progress);
            }
        }

        private void WriteToDirectoryInternal(
            string destinationDirectory,
            IProgress<ProgressReport>? progress
        )
        {
            var totalBytes = archive.TotalUncompressedSize;
            var bytesRead = 0L;
            var seenDirectories = new HashSet<string>();

            foreach (var entry in archive.Entries)
            {
                if (entry.IsDirectory)
                {
                    var dirPath = Path.Combine(
                        destinationDirectory,
                        entry.Key.NotNull("Entry Key is null")
                    );
                    if (
                        Path.GetDirectoryName(dirPath + "/") is { } parentDirectory
                        && seenDirectories.Add(dirPath)
                    )
                    {
                        Directory.CreateDirectory(parentDirectory);
                    }
                    continue;
                }

                entry.WriteToDirectory(destinationDirectory);

                bytesRead += entry.Size;
                progress?.Report(
                    new ProgressReport(entry.Key ?? string.Empty, bytesRead, totalBytes)
                );
            }
        }
    }
}
