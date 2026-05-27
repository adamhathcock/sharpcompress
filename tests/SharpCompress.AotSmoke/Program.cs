using System;
using System.IO;
using System.Text;
using SharpCompress.Archives;
using SharpCompress.Common;
using SharpCompress.Readers;
using SharpCompress.Writers;

var original = "SharpCompress AOT smoke test";
using var archiveStream = new MemoryStream();

using (
    var writer = WriterFactory.OpenWriter(
        archiveStream,
        ArchiveType.Zip,
        new WriterOptions(CompressionType.Deflate) { LeaveStreamOpen = true }
    )
)
{
    using var entryStream = new MemoryStream(Encoding.UTF8.GetBytes(original));
    writer.Write("payload.txt", entryStream, DateTime.UtcNow);
}

archiveStream.Position = 0;
using (var reader = ReaderFactory.OpenReader(archiveStream, ReaderOptions.ForExternalStream))
{
    if (!reader.MoveToNextEntry() || reader.Entry.IsDirectory)
    {
        throw new InvalidOperationException("Expected a file entry.");
    }

    using var extracted = new MemoryStream();
    reader.WriteEntryTo(extracted);
    var actual = Encoding.UTF8.GetString(extracted.ToArray());
    if (!string.Equals(original, actual, StringComparison.Ordinal))
    {
        throw new InvalidOperationException("ReaderFactory round-trip content mismatch.");
    }
}

archiveStream.Position = 0;
using (var archive = ArchiveFactory.OpenArchive(archiveStream, ReaderOptions.ForExternalStream))
{
    var entryCount = 0;
    foreach (var entry in archive.Entries)
    {
        if (!entry.IsDirectory)
        {
            entryCount++;
        }
    }

    if (entryCount != 1)
    {
        throw new InvalidOperationException("ArchiveFactory did not see the expected entry.");
    }
}

Console.WriteLine("SharpCompress AOT smoke test passed.");
