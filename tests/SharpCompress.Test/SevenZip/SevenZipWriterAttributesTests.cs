using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using SharpCompress.Archives;
using SharpCompress.Common;
using SharpCompress.Common.SevenZip;
using SharpCompress.Readers;
using SharpCompress.Writers;
using SharpCompress.Writers.SevenZip;
using Xunit;

namespace SharpCompress.Test.SevenZip;

public class SevenZipWriterAttributesTests
{
    [Theory]
    [InlineData(false, false, 2)]
    [InlineData(false, true, 2)]
    [InlineData(true, false, 2)]
    [InlineData(true, true, 2)]
    [InlineData(false, false, 11)]
    [InlineData(false, true, 11)]
    [InlineData(true, false, 11)]
    [InlineData(true, true, 11)]
    public async Task SevenZipWriter_MixedEntries_AttributesAreAllDefined(
        bool useAsync,
        bool compressHeader,
        int count
    )
    {
        using var stream = new MemoryStream();
        var options = new SevenZipWriterOptions(CompressionType.LZMA2)
        {
            CompressHeader = compressHeader,
            LeaveStreamOpen = true,
        };
        var modificationTime = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var content = "hello"u8.ToArray();

        if (useAsync)
        {
            await using var writer = await WriterFactory.OpenAsyncWriter(
                stream,
                ArchiveType.SevenZip,
                options,
                CancellationToken.None
            );
            await writer.WriteDirectoryAsync("dir", modificationTime, CancellationToken.None);
            for (var i = 1; i < count; i++)
            {
                using var source = new MemoryStream(i == 1 && count > 2 ? [] : content);
                await writer.WriteAsync(
                    $"dir/file{i}.txt",
                    source,
                    modificationTime,
                    CancellationToken.None
                );
            }
        }
        else
        {
            using var writer = WriterFactory.OpenWriter(stream, ArchiveType.SevenZip, options);
            writer.WriteDirectory("dir", modificationTime);
            for (var i = 1; i < count; i++)
            {
                using var source = new MemoryStream(i == 1 && count > 2 ? [] : content);
                writer.Write($"dir/file{i}.txt", source, modificationTime);
            }
        }

        Assert.True(stream.CanRead);
        if (!compressHeader)
        {
            // Check the wire representation, not just a SharpCompress round trip:
            // its reader also accepts the partial bitmap that libarchive rejects.
            using var expected = new MemoryStream();
            expected.WriteByte(0x15);
            expected.WriteEncodedUInt64((ulong)(count * 4 + 2));
            expected.WriteByte(1); // AllAreDefined: no bitmap follows.
            expected.WriteByte(0); // Inline attributes.
            using (var binaryWriter = new BinaryWriter(expected, System.Text.Encoding.UTF8, true))
            {
                for (var i = 0; i < count; i++)
                {
                    binaryWriter.Write(i == 0 ? 0x10u : 0x20u);
                }
            }
            expected.WriteByte(0); // FilesInfo end.
            expected.WriteByte(0); // Header end.
            var suffix = expected.ToArray();
            var bytes = stream.ToArray();
            Assert.Equal(suffix, bytes.Skip(bytes.Length - suffix.Length).ToArray());
        }

        stream.Position = 0;
        if (useAsync)
        {
            await using var archive = await ArchiveFactory.OpenAsyncArchive(
                stream,
                ReaderOptions.ForExternalStream,
                CancellationToken.None
            );
            var entries = new List<IArchiveEntry>();
            await foreach (
                var entry in archive.EntriesAsync.WithCancellation(CancellationToken.None)
            )
            {
                entries.Add(entry);
            }
            Assert.Equal(count, entries.Count);
            for (var i = 0; i < count; i++)
            {
                var entry = entries[i];
                Assert.Equal(i == 0 ? "dir" : $"dir/file{i}.txt", entry.Key);
                Assert.Equal(i == 0, entry.IsDirectory);
                Assert.Equal(i == 0 ? 0x10 : 0x20, entry.Attrib);
                if (i == 0)
                {
                    continue;
                }
                using var source = await entry.OpenEntryStreamAsync(CancellationToken.None);
                using var output = new MemoryStream();
                await source.CopyToAsync(output, 81920, CancellationToken.None);
                Assert.Equal(i == 1 && count > 2 ? [] : content, output.ToArray());
            }
        }
        else
        {
            using var archive = ArchiveFactory.OpenArchive(stream, ReaderOptions.ForExternalStream);
            var entries = archive.Entries.ToList();
            Assert.Equal(count, entries.Count);
            for (var i = 0; i < count; i++)
            {
                var entry = entries[i];
                Assert.Equal(i == 0 ? "dir" : $"dir/file{i}.txt", entry.Key);
                Assert.Equal(i == 0, entry.IsDirectory);
                Assert.Equal(i == 0 ? 0x10 : 0x20, entry.Attrib);
                if (i == 0)
                {
                    continue;
                }
                using var source = entry.OpenEntryStream();
                using var output = new MemoryStream();
                source.CopyTo(output);
                Assert.Equal(i == 1 && count > 2 ? [] : content, output.ToArray());
            }
        }
    }

    [Theory]
    [InlineData(2)]
    [InlineData(10)]
    public void SevenZipFilesInfo_MixedEntries_AttributesAreAllDefined(int count)
    {
        var entries = new SevenZipWriteEntry[count];
        for (var i = 0; i < count; i++)
        {
            entries[i] =
                i == 0
                    ? new SevenZipWriteEntry
                    {
                        Name = "dir",
                        IsDirectory = true,
                        IsEmpty = true,
                        Attributes = 0x10,
                    }
                    : new SevenZipWriteEntry { Name = $"file{i}", IsEmpty = true };
        }
        using var stream = new MemoryStream();
        new SevenZipFilesInfoWriter { Entries = entries }.Write(stream);
        var bytes = stream.ToArray();
        // Attributes are the final property: id, size, all-defined, external, values, end.
        var start = bytes.Length - (count * 4 + 5);
        Assert.Equal(0x15, bytes[start]);
        Assert.Equal(count * 4 + 2, bytes[start + 1]);
        Assert.Equal(1, bytes[start + 2]);
        Assert.Equal(0, bytes[start + 3]);
        for (var i = 0; i < count; i++)
        {
            Assert.Equal(
                i == 0 ? 0x10u : 0x20u,
                System.BitConverter.ToUInt32(bytes, start + 4 + i * 4)
            );
        }
    }
}
