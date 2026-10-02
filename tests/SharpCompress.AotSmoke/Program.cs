using System;
using System.IO;
using System.Text;
using System.Threading;
using SharpCompress;
using SharpCompress.Archives;
using SharpCompress.Common;
using SharpCompress.Readers;
using SharpCompress.Writers;

var original = "SharpCompress AOT smoke test";
var client = new SharpCompressClient();
using var archiveStream = new MemoryStream();

using (
    var writer = client.OpenWriter(
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
using (var reader = client.OpenReader(archiveStream, ReaderOptions.ForExternalStream))
{
    if (!reader.MoveToNextEntry() || reader.Entry.IsDirectory)
    {
        throw new InvalidOperationException("Expected a file entry.");
    }

    using var extracted = new MemoryStream();
    client.Extractor.ExtractToStream(reader, extracted);
    var actual = Encoding.UTF8.GetString(extracted.ToArray());
    if (!string.Equals(original, actual, StringComparison.Ordinal))
    {
        throw new InvalidOperationException("Client reader round-trip content mismatch.");
    }
}

archiveStream.Position = 0;
using (var archive = client.OpenArchive(archiveStream, ReaderOptions.ForExternalStream))
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
        throw new InvalidOperationException("Client archive did not see the expected entry.");
    }
}

archiveStream.Position = 0;
var information = await client.Inspector.InspectArchiveAsync(
    archiveStream,
    cancellationToken: CancellationToken.None
);
if (information?.EntryCount != 1 || !archiveStream.CanRead || archiveStream.Position != 0)
{
    throw new InvalidOperationException("Client inspection did not preserve the source.");
}

await using (
    var archive = await client.OpenAsyncArchive(
        archiveStream,
        cancellationToken: CancellationToken.None
    )
)
{
    await foreach (var entry in archive.EntriesAsync)
    {
        using var extracted = new MemoryStream();
        await client.Extractor.ExtractToStreamAsync(
            entry,
            extracted,
            cancellationToken: CancellationToken.None
        );
        if (Encoding.UTF8.GetString(extracted.ToArray()) != original)
        {
            throw new InvalidOperationException("Async client extraction content mismatch.");
        }
    }
}

Console.WriteLine("SharpCompress AOT smoke test passed.");
