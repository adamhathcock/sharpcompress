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
