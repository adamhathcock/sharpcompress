using System;
using System.IO;
using SharpCompress.Common;
using SharpCompress.IO;

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
        public void WriteAllToDirectory(
            string destinationDirectory,
            ExtractionOptions? options = null
        )
        {
            while (reader.MoveToNextEntry())
            {
                reader.WriteEntryToDirectory(destinationDirectory, options);
            }
        }

        /// <summary>
        /// Extract to specific directory, retaining filename
        /// </summary>
        public void WriteEntryToDirectory(
            string destinationDirectory,
            ExtractionOptions? options = null
        ) =>
            reader.Entry.WriteEntryToDirectory(
                destinationDirectory,
                options,
                (path) => reader.WriteEntryToFile(path, options)
            );

        /// <summary>
        /// Extract to specific file
        /// </summary>
        public void WriteEntryToFile(
            string destinationFileName,
            ExtractionOptions? options = null
        ) =>
            reader.Entry.WriteEntryToFile(
                destinationFileName,
                options,
                (x, fm) =>
                {
                    using var fs = File.Open(x, fm);
                    CopyEntryTo(reader, fs, options?.BufferSize ?? Constants.BufferSize);
                }
            );
    }

    private static void CopyEntryTo(IReader reader, Stream writableStream, int bufferSize)
    {
        using var entryStream = reader.OpenEntryStream();
        var sourceStream = WrapWithProgress(entryStream, reader.Entry);
        sourceStream.CopyTo(writableStream, bufferSize);
    }

    private static Stream WrapWithProgress(Stream source, IEntry entry)
    {
        var progress = entry.Options.Progress;
        if (progress is null)
        {
            return source;
        }

        var entryPath = entry.Key ?? string.Empty;
        var totalBytes = GetEntrySizeSafe(entry);
        return new ProgressReportingStream(
            source,
            progress,
            entryPath,
            totalBytes,
            leaveOpen: true
        );
    }

    private static long? GetEntrySizeSafe(IEntry entry)
    {
        try
        {
            var size = entry.Size;
            return size >= 0 ? size : null;
        }
        catch (NotImplementedException)
        {
            return null;
        }
    }
}
