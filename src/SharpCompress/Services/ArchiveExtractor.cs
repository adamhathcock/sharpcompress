using System;
using Pure.DI;
using SharpCompress.Archives;
using SharpCompress.Common;
using SharpCompress.Readers;

namespace SharpCompress.Services;

[GenerateInterface(asInternal: true)]
internal sealed partial class ArchiveExtractor(
    IArchiveService archives,
    IReaderExtractor readers,
    IArchiveEntryExtractor entries
) : IArchiveExtractor
{
    public void WriteToDirectory(
        string sourceArchive,
        string destinationDirectory,
        ExtractionOptions? options = null
    )
    {
        using var archive = archives.OpenArchive(sourceArchive);
        WriteToDirectory(archive, destinationDirectory, options);
    }

    /// <summary>
    /// Extract to specific directory with progress reporting
    /// </summary>
    /// <param name="destinationDirectory">The folder to extract into.</param>
    /// <param name="options">Extraction options.</param>
    /// <param name="progress">Optional progress reporter for tracking extraction progress.</param>
    public void WriteToDirectory(
        IArchive archive,
        string destinationDirectory,
        ExtractionOptions? options = null,
        IProgress<ProgressReport>? progress = null
    )
    {
        if (archive.IsSolid || archive.Type == ArchiveType.SevenZip)
        {
            using var reader = archive.ExtractAllEntries();
            readers.WriteAllToDirectory(reader, destinationDirectory, options);
        }
        else
        {
            WriteToDirectoryInternal(archive, destinationDirectory, options, progress);
        }
    }

    private void WriteToDirectoryInternal(
        IArchive archive,
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
                entry.WriteEntryToDirectoryCore(fullDestinationDirectoryPath, options, null);
                continue;
            }

            entry.WriteEntryToDirectoryCore(
                fullDestinationDirectoryPath,
                options,
                path => entries.WriteToFile(entry, path, options)
            );

            bytesRead += entry.Size;
            progress?.Report(new ProgressReport(entry.Key ?? string.Empty, bytesRead, totalBytes));
        }
    }
}
