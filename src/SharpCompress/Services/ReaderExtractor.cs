using System.IO;
using Pure.DI;
using SharpCompress.Common;
using SharpCompress.Readers;

namespace SharpCompress.Services;

[GenerateInterface(asInternal: true)]
internal sealed partial class ReaderExtractor : IReaderExtractor
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
        var sourceStream = ExtractionStreamHelper.WrapWithProgress(
            checkedStream,
            reader.Entry,
            reader.Entry.Options.Progress
        );
        sourceStream.CopyTo(writableStream, options.BufferSize);
    }
}
