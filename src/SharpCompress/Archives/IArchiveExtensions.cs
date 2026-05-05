using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using SharpCompress.Common;
using SharpCompress.Readers;

namespace SharpCompress.Archives;

public static class IArchiveExtensions
{

    /// <summary>
    /// Gets the appropriate StringComparison for path checks based on the file system.
    /// Windows uses case-insensitive file systems, while Unix-like systems use case-sensitive file systems.
    /// </summary>
    private static StringComparison PathComparison =>
        RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
            ? StringComparison.OrdinalIgnoreCase
            : StringComparison.Ordinal;
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
            var fullDestinationDirectoryPath = Path.GetFullPath(destinationDirectory);
            options ??= new ExtractionOptions();

            //check for trailing slash.
            if (
                fullDestinationDirectoryPath[fullDestinationDirectoryPath.Length - 1]
                != Path.DirectorySeparatorChar
            )
            {
                fullDestinationDirectoryPath += Path.DirectorySeparatorChar;
            }

            if (!Directory.Exists(fullDestinationDirectoryPath))
            {
                throw new ExtractionException(
                    $"Directory does not exist to extract to: {fullDestinationDirectoryPath}"
                );
            }

            var totalBytes = archive.TotalUncompressedSize;
            var bytesRead = 0L;
            var seenDirectories = new HashSet<string>();

            foreach (var entry in archive.Entries)
            {
                if (entry.IsDirectory)
                {

                    var folder = Path.GetDirectoryName(entry.Key.NotNull("Entry Key is null"))
                                     .NotNull("Directory is null");
                    var destdir = Path.GetFullPath(Path.Combine(fullDestinationDirectoryPath, folder));

                    if (!Directory.Exists(destdir) &&  seenDirectories.Add(destdir))
                    {
                        if (!destdir.StartsWith(fullDestinationDirectoryPath, PathComparison))
                        {
                            throw new ExtractionException(
                                "Entry is trying to create a directory outside of the destination directory."
                            );
                        }

                        Directory.CreateDirectory(destdir);
                    }
                    continue;
                }

                entry.WriteToDirectory(destinationDirectory, options);

                bytesRead += entry.Size;
                progress?.Report(
                    new ProgressReport(entry.Key ?? string.Empty, bytesRead, totalBytes)
                );
            }
        }
    }
}
