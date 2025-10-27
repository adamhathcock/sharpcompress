using System.IO;
using System.Linq;
using System.Threading.Tasks;
using SharpCompress.Archives;
using SharpCompress.Archives.GZip;
using SharpCompress.Archives.Tar;
using SharpCompress.Common;
using Xunit;

namespace SharpCompress.Test.GZip;

public class GZipArchiveAsyncTests : ArchiveTests
{
    public GZipArchiveAsyncTests() => UseExtensionInsteadOfNameToVerify = true;

    [Fact]
    public async Task GZip_Archive_Generic_Async()
    {
        using (Stream stream = File.OpenRead(Path.Combine(TEST_ARCHIVES_PATH, "Tar.tar.gz")))
        using (var archive = ArchiveFactory.Open(stream))
        {
            var entry = archive.Entries.First();
            await entry.WriteToFileAsync(Path.Combine(SCRATCH_FILES_PATH, entry.Key.NotNull()));

            var size = entry.Size;
            var scratch = new FileInfo(Path.Combine(SCRATCH_FILES_PATH, "Tar.tar"));
            var test = new FileInfo(Path.Combine(TEST_ARCHIVES_PATH, "Tar.tar"));

            Assert.Equal(size, scratch.Length);
            Assert.Equal(size, test.Length);
        }
        CompareArchivesByPath(
            Path.Combine(SCRATCH_FILES_PATH, "Tar.tar"),
            Path.Combine(TEST_ARCHIVES_PATH, "Tar.tar")
        );
    }

    [Fact]
    public async Task GZip_Archive_Async()
    {
        using (Stream stream = File.OpenRead(Path.Combine(TEST_ARCHIVES_PATH, "Tar.tar.gz")))
        using (var archive = GZipArchive.Open(stream))
        {
            var entry = archive.Entries.First();
            await entry.WriteToFileAsync(Path.Combine(SCRATCH_FILES_PATH, entry.Key.NotNull()));

            var size = entry.Size;
            var scratch = new FileInfo(Path.Combine(SCRATCH_FILES_PATH, "Tar.tar"));
            var test = new FileInfo(Path.Combine(TEST_ARCHIVES_PATH, "Tar.tar"));

            Assert.Equal(size, scratch.Length);
            Assert.Equal(size, test.Length);
        }
        CompareArchivesByPath(
            Path.Combine(SCRATCH_FILES_PATH, "Tar.tar"),
            Path.Combine(TEST_ARCHIVES_PATH, "Tar.tar")
        );
    }

    [Fact]
    public async Task GZip_Archive_NoAdd_Async()
    {
        var jpg = Path.Combine(ORIGINAL_FILES_PATH, "jpg", "test.jpg");
        using Stream stream = File.OpenRead(Path.Combine(TEST_ARCHIVES_PATH, "Tar.tar.gz"));
        using var archive = GZipArchive.Open(stream);
        Assert.Throws<InvalidFormatException>(() => archive.AddEntry("jpg\\test.jpg", jpg));
        await archive.SaveToAsync(Path.Combine(SCRATCH_FILES_PATH, "Tar.tar.gz"));
    }

    [Fact]
    public async Task GZip_Archive_Multiple_Reads_Async()
    {
        var inputStream = new MemoryStream();
        using (var fileStream = File.OpenRead(Path.Combine(TEST_ARCHIVES_PATH, "Tar.tar.gz")))
        {
            await fileStream.CopyToAsync(inputStream);
            inputStream.Position = 0;
        }
        using var archive = GZipArchive.Open(inputStream);
        var archiveEntry = archive.Entries.First();

        MemoryStream tarStream;
        using (var entryStream = archiveEntry.OpenEntryStream())
        {
            tarStream = new MemoryStream();
            await entryStream.CopyToAsync(tarStream);
        }
        var size = tarStream.Length;
        using (var entryStream = archiveEntry.OpenEntryStream())
        {
            tarStream = new MemoryStream();
            await entryStream.CopyToAsync(tarStream);
        }
        Assert.Equal(size, tarStream.Length);
        using (var entryStream = archiveEntry.OpenEntryStream())
        {
            var result = TarArchive.IsTarFile(entryStream);
            Assert.True(result);
        }
        Assert.Equal(size, tarStream.Length);
        using (var entryStream = archiveEntry.OpenEntryStream())
        {
            tarStream = new MemoryStream();
            await entryStream.CopyToAsync(tarStream);
        }
        Assert.Equal(size, tarStream.Length);
    }

    [Fact]
    public void TestGzCrcWithMostSignificantBitNotNegative_Async()
    {
        using var stream = File.OpenRead(Path.Combine(TEST_ARCHIVES_PATH, "Tar.tar.gz"));
        using var archive = GZipArchive.Open(stream);
        foreach (var entry in archive.Entries.Where(entry => !entry.IsDirectory))
        {
            Assert.InRange(entry.Crc, 0L, 0xFFFFFFFFL);
        }
    }

    [Fact]
    public void TestGzArchiveTypeGzip_Async()
    {
        using var stream = File.OpenRead(Path.Combine(TEST_ARCHIVES_PATH, "Tar.tar.gz"));
        using var archive = GZipArchive.Open(stream);
        Assert.Equal(archive.Type, ArchiveType.GZip);
    }
}
