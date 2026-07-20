using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using SharpCompress.Archives.Zip;
using SharpCompress.Common;
using SharpCompress.Test.Mocks;
using SharpCompress.Writers;
using SharpCompress.Writers.Zip;
using Xunit;

namespace SharpCompress.Test.Zip;

/// <summary>
/// Regression tests for writing zips to non-seekable output streams, where entries must be
/// written in streaming layout: data-descriptor flag (bit 3) set in the local header and
/// central directory, zeroed local CRC/sizes, and a trailing PK\x07\x08 descriptor per entry.
/// The async writer used to derive the flag from its internal buffering MemoryStream instead
/// of the real output, producing archives that strict readers reject.
/// </summary>
public class ZipWriterNonSeekableTests
{
    private static readonly DateTime FixedModificationTime = new(2024, 5, 15, 10, 30, 0);

    private static (string Name, byte[] Content)[] CreateStreamingTestEntries() =>
        new[]
        {
            (
                "first.txt",
                Encoding.UTF8.GetBytes(string.Concat(Enumerable.Repeat("Hello streaming zip! ", 500)))
            ),
            (
                "nested/second.txt",
                Encoding.UTF8.GetBytes(string.Concat(Enumerable.Repeat("Another entry with different content. ", 300)))
            ),
        };

    private static async Task<byte[]> WriteArchiveToNonSeekableAsync(
        (string Name, byte[] Content)[] entries
    )
    {
        using var ms = new MemoryStream();
        await using (
            var writer = await WriterFactory.OpenAsyncWriter(
                new ForwardOnlyStream(ms),
                ArchiveType.Zip,
                new ZipWriterOptions(CompressionType.Deflate)
            )
        )
        {
            foreach (var (name, content) in entries)
            {
                using var source = new MemoryStream(content);
                await writer.WriteAsync(name, source, FixedModificationTime);
            }
        }
        return ms.ToArray();
    }

    private static byte[] WriteArchiveToNonSeekableSync((string Name, byte[] Content)[] entries)
    {
        using var ms = new MemoryStream();
        using (
            var writer = WriterFactory.OpenWriter(
                new ForwardOnlyStream(ms),
                ArchiveType.Zip,
                new ZipWriterOptions(CompressionType.Deflate)
            )
        )
        {
            foreach (var (name, content) in entries)
            {
                using var source = new MemoryStream(content);
                writer.Write(name, source, FixedModificationTime);
            }
        }
        return ms.ToArray();
    }

    private static ushort ReadUInt16(byte[] data, int offset) =>
        (ushort)(data[offset] | (data[offset + 1] << 8));

    private static uint ReadUInt32(byte[] data, int offset) =>
        (uint)(
            data[offset]
            | (data[offset + 1] << 8)
            | (data[offset + 2] << 16)
            | (data[offset + 3] << 24)
        );

    /// <summary>
    /// Structurally validates a streamed (non-seekable output) zip: every local header and
    /// central directory record must have the data-descriptor flag (bit 3, 0x0008) set, local
    /// CRC/size fields must be zeroed (deferred), and a PK\x07\x08 descriptor with values
    /// matching the central directory must follow each entry's data.
    /// </summary>
    private static void AssertStreamedZipLayout(byte[] zip, int expectedEntries)
    {
        // End of central directory record; the archive has no comment, so it is the last 22 bytes
        var eocd = zip.Length - 22;
        Assert.Equal(0x06054b50u, ReadUInt32(zip, eocd));
        int entryCount = ReadUInt16(zip, eocd + 10);
        Assert.Equal(expectedEntries, entryCount);
        var cdOffset = checked((int)ReadUInt32(zip, eocd + 16));

        var pos = cdOffset;
        for (var i = 0; i < entryCount; i++)
        {
            Assert.Equal(0x02014b50u, ReadUInt32(zip, pos));
            var cdFlags = ReadUInt16(zip, pos + 8);
            Assert.True(
                (cdFlags & 0x0008) != 0,
                $"entry {i}: central directory flags 0x{cdFlags:x4} lack the data-descriptor bit"
            );
            var crc = ReadUInt32(zip, pos + 16);
            var compressedSize = ReadUInt32(zip, pos + 20);
            var uncompressedSize = ReadUInt32(zip, pos + 24);
            var nameLength = ReadUInt16(zip, pos + 28);
            var extraLength = ReadUInt16(zip, pos + 30);
            var commentLength = ReadUInt16(zip, pos + 32);
            var localHeaderOffset = checked((int)ReadUInt32(zip, pos + 42));

            Assert.Equal(0x04034b50u, ReadUInt32(zip, localHeaderOffset));
            var localFlags = ReadUInt16(zip, localHeaderOffset + 6);
            Assert.True(
                (localFlags & 0x0008) != 0,
                $"entry {i}: local header flags 0x{localFlags:x4} lack the data-descriptor bit"
            );
            Assert.Equal(
                cdFlags,
                localFlags
            );
            Assert.Equal(0u, ReadUInt32(zip, localHeaderOffset + 14)); // deferred CRC
            Assert.Equal(0u, ReadUInt32(zip, localHeaderOffset + 18)); // deferred compressed size
            Assert.Equal(0u, ReadUInt32(zip, localHeaderOffset + 22)); // deferred uncompressed size

            var localNameLength = ReadUInt16(zip, localHeaderOffset + 26);
            var localExtraLength = ReadUInt16(zip, localHeaderOffset + 28);
            var descriptor =
                localHeaderOffset + 30 + localNameLength + localExtraLength + (int)compressedSize;
            Assert.Equal(0x08074b50u, ReadUInt32(zip, descriptor));
            Assert.Equal(crc, ReadUInt32(zip, descriptor + 4));
            Assert.Equal(compressedSize, ReadUInt32(zip, descriptor + 8));
            Assert.Equal(uncompressedSize, ReadUInt32(zip, descriptor + 12));

            pos += 46 + nameLength + extraLength + commentLength;
        }
    }

    [Fact]
    public async Task Zip_Async_NonSeekable_Writes_DataDescriptor_Flags()
    {
        var entries = CreateStreamingTestEntries();
        var zip = await WriteArchiveToNonSeekableAsync(entries);

        AssertStreamedZipLayout(zip, entries.Length);

        // Round-trip: the archive must be readable and contents intact
        using var archive = ZipArchive.OpenArchive(new MemoryStream(zip));
        Assert.Equal(entries.Length, archive.Entries.Count());
        foreach (var (name, content) in entries)
        {
            var entry = archive.Entries.Single(e => e.Key == name);
            using var extracted = new MemoryStream();
            using (var entryStream = entry.OpenEntryStream())
            {
                entryStream.CopyTo(extracted);
            }
            Assert.Equal(content, extracted.ToArray());
        }
    }

    [Fact]
    public async Task Zip_Async_NonSeekable_Matches_Sync_Output()
    {
        var entries = CreateStreamingTestEntries();
        var asyncZip = await WriteArchiveToNonSeekableAsync(entries);
        var syncZip = WriteArchiveToNonSeekableSync(entries);

        // The sync writer produces a correct streamed layout; the async writer must match it
        Assert.Equal(syncZip, asyncZip);
    }
}
