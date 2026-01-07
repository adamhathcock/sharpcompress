using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using SharpCompress.Archives.Zip;
using SharpCompress.Common;
using SharpCompress.Common.Zip.SOZip;
using SharpCompress.Readers.Zip;
using SharpCompress.Test.Mocks;
using SharpCompress.Writers;
using SharpCompress.Writers.Zip;
using Xunit;

namespace SharpCompress.Test.Zip;

public class SoZipReaderTests : TestBase
{
    [Fact]
    public async Task SOZip_Reader_RegularZip_NoSozipEntries()
    {
        // Regular zip files should not have SOZip entries
        var path = Path.Combine(TEST_ARCHIVES_PATH, "Zip.deflate.zip");
        using Stream stream = new ForwardOnlyStream(File.OpenRead(path));
        using var reader = ZipReader.Open(stream);
        while (await reader.MoveToNextEntryAsync())
        {
            // Regular zip entries should NOT be SOZip
            Assert.False(reader.Entry.IsSozip, $"Entry {reader.Entry.Key} should not be SOZip");
            Assert.False(
                reader.Entry.IsSozipIndexFile,
                $"Entry {reader.Entry.Key} should not be a SOZip index file"
            );
        }
    }

    [Fact]
    public void SOZip_Archive_RegularZip_NoSozipEntries()
    {
        // Regular zip files should not have SOZip entries
        var path = Path.Combine(TEST_ARCHIVES_PATH, "Zip.deflate.zip");
        using Stream stream = File.OpenRead(path);
        using var archive = ZipArchive.Open(stream);
        foreach (var entry in archive.Entries)
        {
            // Regular zip entries should NOT be SOZip
            Assert.False(entry.IsSozip, $"Entry {entry.Key} should not be SOZip");
            Assert.False(
                entry.IsSozipIndexFile,
                $"Entry {entry.Key} should not be a SOZip index file"
            );
        }
    }

    [Fact]
    public void SOZip_Archive_ReadSOZipFile()
    {
        // Read the SOZip test archive
        var path = Path.Combine(TEST_ARCHIVES_PATH, "Zip.sozip.zip");
        using Stream stream = File.OpenRead(path);
        using var archive = ZipArchive.Open(stream);

        var entries = archive.Entries.ToList();

        // Should have 3 entries: data.txt, .data.txt.sozip.idx, and small.txt
        Assert.Equal(3, entries.Count);

        // Verify we have one SOZip index file
        var indexFiles = entries.Where(e => e.IsSozipIndexFile).ToList();
        Assert.Single(indexFiles);
        Assert.Equal(".data.txt.sozip.idx", indexFiles[0].Key);

        // Verify the index file is not compressed
        Assert.Equal(CompressionType.None, indexFiles[0].CompressionType);

        // Read and validate the index
        using (var indexStream = indexFiles[0].OpenEntryStream())
        {
            using var memStream = new MemoryStream();
            indexStream.CopyTo(memStream);
            var indexBytes = memStream.ToArray();

            var index = SOZipIndex.Read(indexBytes);
            Assert.Equal(SOZipIndex.SOZIP_VERSION, index.Version);
            Assert.Equal(1024u, index.ChunkSize); // As set in CreateSOZipTestArchive
            Assert.True(index.UncompressedSize > 0);
            Assert.True(index.OffsetCount > 0);
        }

        // Verify the data file can be read correctly
        var dataEntry = entries.First(e => e.Key == "data.txt");
        using (var dataStream = dataEntry.OpenEntryStream())
        {
            using var reader = new StreamReader(dataStream);
            var content = reader.ReadToEnd();
            Assert.Equal(5000, content.Length);
            Assert.True(content.All(c => c == 'A'));
        }

        // Verify the small file
        var smallEntry = entries.First(e => e.Key == "small.txt");
        Assert.False(smallEntry.IsSozipIndexFile);
        using (var smallStream = smallEntry.OpenEntryStream())
        {
            using var reader = new StreamReader(smallStream);
            var content = reader.ReadToEnd();
            Assert.Equal("Small content", content);
        }
    }

    [Fact]
    public async Task SOZip_Reader_ReadSOZipFile()
    {
        // Read the SOZip test archive with ZipReader
        var path = Path.Combine(TEST_ARCHIVES_PATH, "Zip.sozip.zip");
        using Stream stream = new ForwardOnlyStream(File.OpenRead(path));
        using var reader = ZipReader.Open(stream);

        var foundData = false;
        var foundIndex = false;
        var foundSmall = false;

        while (await reader.MoveToNextEntryAsync())
        {
            if (reader.Entry.Key == "data.txt")
            {
                foundData = true;
                Assert.False(reader.Entry.IsSozipIndexFile);

                using var entryStream = reader.OpenEntryStream();
                using var streamReader = new StreamReader(entryStream);
                var content = streamReader.ReadToEnd();
                Assert.Equal(5000, content.Length);
                Assert.True(content.All(c => c == 'A'));
            }
            else if (reader.Entry.Key == ".data.txt.sozip.idx")
            {
                foundIndex = true;
                Assert.True(reader.Entry.IsSozipIndexFile);

                using var indexStream = reader.OpenEntryStream();
                using var memStream = new MemoryStream();
                await indexStream.CopyToAsync(memStream);
                var indexBytes = memStream.ToArray();

                var index = SOZipIndex.Read(indexBytes);
                Assert.Equal(SOZipIndex.SOZIP_VERSION, index.Version);
            }
            else if (reader.Entry.Key == "small.txt")
            {
                foundSmall = true;
                Assert.False(reader.Entry.IsSozipIndexFile);
            }
        }

        Assert.True(foundData, "data.txt entry not found");
        Assert.True(foundIndex, ".data.txt.sozip.idx entry not found");
        Assert.True(foundSmall, "small.txt entry not found");
    }

    [Fact]
    public void SOZip_Archive_DetectsIndexFileByName()
    {
        // Create a zip with a SOZip index file (by name pattern)
        using var memoryStream = new MemoryStream();

        using (
            var writer = WriterFactory.Open(
                memoryStream,
                ArchiveType.Zip,
                new ZipWriterOptions(CompressionType.Deflate) { LeaveStreamOpen = true }
            )
        )
        {
            // Write a regular file
            writer.Write("test.txt", new MemoryStream(Encoding.UTF8.GetBytes("Hello World")));

            // Write a file that looks like a SOZip index (by name pattern)
            var indexData = new SOZipIndex(
                chunkSize: 32768,
                uncompressedSize: 100,
                compressedSize: 50,
                compressedOffsets: new ulong[] { 0 }
            );
            writer.Write(".test.txt.sozip.idx", new MemoryStream(indexData.ToByteArray()));
        }

        memoryStream.Position = 0;

        // Test with ZipArchive
        using var archive = ZipArchive.Open(memoryStream);
        var entries = archive.Entries.ToList();

        Assert.Equal(2, entries.Count);

        var regularEntry = entries.First(e => e.Key == "test.txt");
        Assert.False(regularEntry.IsSozipIndexFile);
        Assert.False(regularEntry.IsSozip); // No SOZip extra field

        var indexEntry = entries.First(e => e.Key == ".test.txt.sozip.idx");
        Assert.True(indexEntry.IsSozipIndexFile);
    }

    [Fact]
    public async Task SOZip_Reader_DetectsIndexFileByName()
    {
        // Create a zip with a SOZip index file (by name pattern)
        using var memoryStream = new MemoryStream();

        using (
            var writer = WriterFactory.Open(
                memoryStream,
                ArchiveType.Zip,
                new ZipWriterOptions(CompressionType.Deflate) { LeaveStreamOpen = true }
            )
        )
        {
            // Write a regular file
            writer.Write("test.txt", new MemoryStream(Encoding.UTF8.GetBytes("Hello World")));

            // Write a file that looks like a SOZip index (by name pattern)
            var indexData = new SOZipIndex(
                chunkSize: 32768,
                uncompressedSize: 100,
                compressedSize: 50,
                compressedOffsets: new ulong[] { 0 }
            );
            writer.Write(".test.txt.sozip.idx", new MemoryStream(indexData.ToByteArray()));
        }

        memoryStream.Position = 0;

        // Test with ZipReader
        using Stream stream = new ForwardOnlyStream(memoryStream);
        using var reader = ZipReader.Open(stream);

        var foundRegular = false;
        var foundIndex = false;

        while (await reader.MoveToNextEntryAsync())
        {
            if (reader.Entry.Key == "test.txt")
            {
                foundRegular = true;
                Assert.False(reader.Entry.IsSozipIndexFile);
                Assert.False(reader.Entry.IsSozip);
            }
            else if (reader.Entry.Key == ".test.txt.sozip.idx")
            {
                foundIndex = true;
                Assert.True(reader.Entry.IsSozipIndexFile);
            }
        }

        Assert.True(foundRegular, "Regular entry not found");
        Assert.True(foundIndex, "Index entry not found");
    }
}
