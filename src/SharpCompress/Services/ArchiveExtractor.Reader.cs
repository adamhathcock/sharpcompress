using System;
using System.IO;
using SharpCompress.Common;
using SharpCompress.IO;
using SharpCompress.Readers;

namespace SharpCompress.Services;

internal sealed partial class ArchiveExtractor
{
    public void WriteEntryTo(IReader reader, string filePath)
    {
        using Stream stream = File.Open(filePath, FileMode.Create, FileAccess.Write);
        reader.WriteEntryTo(stream);
    }

    public void WriteEntryTo(IReader reader, FileInfo filePath)
    {
        using Stream stream = filePath.Open(FileMode.Create);
        reader.WriteEntryTo(stream);
    }

    /// <summary>
    /// Extract all remaining unread entries to specific directory, retaining filename
    /// </summary>
    public void WriteAllToDirectory(
        IReader reader,
        string destinationDirectory,
        ExtractionOptions? options = null
    )
    {
        while (reader.MoveToNextEntry())
        {
            WriteEntryToDirectory(reader, destinationDirectory, options);
        }
    }

    /// <summary>
    /// Extract to specific directory, retaining filename
    /// </summary>
    public void WriteEntryToDirectory(
        IReader reader,
        string destinationDirectory,
        ExtractionOptions? options = null
    ) =>
        reader.Entry.WriteEntryToDirectory(
            destinationDirectory,
            options,
            (path) => WriteEntryToFile(reader, path, options)
        );

    /// <summary>
    /// Extract to specific file
    /// </summary>
    public void WriteEntryToFile(
        IReader reader,
        string destinationFileName,
        ExtractionOptions? options = null
    )
    {
        options ??= new ExtractionOptions();
        reader.Entry.WriteEntryToFile(
            destinationFileName,
            options,
            (x, fm) =>
            {
                using var fs = File.Open(x, fm);
                CopyEntryTo(reader, fs, options ?? new ExtractionOptions());
            }
        );
    }

    private static void CopyEntryTo(
        IReader reader,
        Stream writableStream,
        ExtractionOptions options
    )
    {
        using var entryStream = reader.OpenEntryStream();
        var checkedStream = IEntryExtensions.WrapWithChecksumValidation(
            reader.Entry,
            entryStream,
            options
        );
        var sourceStream = WrapWithProgress(checkedStream, reader.Entry);
        sourceStream.CopyTo(writableStream, options.BufferSize);
    }

    private static Stream WrapWithProgress(Stream source, IEntry entry)
    {
        return WrapWithProgress(source, entry, entry.Options.Progress);
    }
}
