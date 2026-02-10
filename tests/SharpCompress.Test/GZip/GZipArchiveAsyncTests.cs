using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using SharpCompress.Archives;
using SharpCompress.Archives.GZip;
using SharpCompress.Archives.Tar;
using SharpCompress.Common;
using SharpCompress.Test.Mocks;
using SharpCompress.Writers.GZip;
using Xunit;

namespace SharpCompress.Test.GZip;

public class GZipArchiveAsyncTests : ArchiveTests
{
    public GZipArchiveAsyncTests() => UseExtensionInsteadOfNameToVerify = true;

    [Fact]
    public async ValueTask GZip_Archive_Generic_Async()
    {
#if NETFRAMEWORK
        using (Stream stream = File.OpenRead(Path.Combine(TEST_ARCHIVES_PATH, "Tar.tar.gz")))
#else
        await using (Stream stream = File.OpenRead(Path.Combine(TEST_ARCHIVES_PATH, "Tar.tar.gz")))
#endif
        await using (var archive = GZipArchive.OpenAsyncArchive(new AsyncOnlyStream(stream)))
        {
            var entry = await archive.EntriesAsync.FirstAsync();
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
    public async ValueTask GZip_Archive_Async()
    {
#if NETFRAMEWORK
        using (Stream stream = File.OpenRead(Path.Combine(TEST_ARCHIVES_PATH, "Tar.tar.gz")))
#else
        await using (Stream stream = File.OpenRead(Path.Combine(TEST_ARCHIVES_PATH, "Tar.tar.gz")))
#endif
        {
            await using (var archive = GZipArchive.OpenAsyncArchive(new AsyncOnlyStream(stream)))
            {
                var entry = await archive.EntriesAsync.FirstAsync();
                await entry.WriteToFileAsync(Path.Combine(SCRATCH_FILES_PATH, entry.Key.NotNull()));

                var size = entry.Size;
                var scratch = new FileInfo(Path.Combine(SCRATCH_FILES_PATH, "Tar.tar"));
                var test = new FileInfo(Path.Combine(TEST_ARCHIVES_PATH, "Tar.tar"));

                Assert.Equal(size, scratch.Length);
                Assert.Equal(size, test.Length);
            }
        }
        CompareArchivesByPath(
            Path.Combine(SCRATCH_FILES_PATH, "Tar.tar"),
            Path.Combine(TEST_ARCHIVES_PATH, "Tar.tar")
        );
    }

    [Fact]
    public async ValueTask GZip_Archive_NoAdd_Async()
    {
        var jpg = Path.Combine(ORIGINAL_FILES_PATH, "jpg", "test.jpg");
#if NETFRAMEWORK
        using Stream stream = File.OpenRead(Path.Combine(TEST_ARCHIVES_PATH, "Tar.tar.gz"));
#else
        await using Stream stream = File.OpenRead(Path.Combine(TEST_ARCHIVES_PATH, "Tar.tar.gz"));
#endif
        await using (var archive = GZipArchive.OpenAsyncArchive(new AsyncOnlyStream(stream)))
        {
            await Assert.ThrowsAsync<NotSupportedException>(async () =>
                await archive.AddEntryAsync("jpg\\test.jpg", File.OpenRead(jpg), closeStream: true)
            );
            await archive.SaveToAsync(
                Path.Combine(SCRATCH_FILES_PATH, "Tar.tar.gz"),
                new GZipWriterOptions()
            );
        }
    }

    [Fact]
    public async ValueTask GZip_Archive_Multiple_Reads_Async()
    {
        var inputStream = new MemoryStream();
#if NETFRAMEWORK
        using (Stream stream = File.OpenRead(Path.Combine(TEST_ARCHIVES_PATH, "Tar.tar.gz")))
#else
        await using (Stream stream = File.OpenRead(Path.Combine(TEST_ARCHIVES_PATH, "Tar.tar.gz")))
#endif
        {
            await stream.CopyToAsync(inputStream);
            inputStream.Position = 0;
        }

        await using var archive = GZipArchive.OpenAsyncArchive(new AsyncOnlyStream(inputStream));
        var archiveEntry = await archive.EntriesAsync.FirstAsync();

        MemoryStream tarStream;
#if NETFRAMEWORK
        using (var entryStream = await archiveEntry.OpenEntryStreamAsync())
#else
        await using (var entryStream = await archiveEntry.OpenEntryStreamAsync())
#endif
        {
            tarStream = new MemoryStream();
            await entryStream.CopyToAsync(tarStream);
        }
        var size = tarStream.Length;
#if NETFRAMEWORK
        using (var entryStream = await archiveEntry.OpenEntryStreamAsync())
#else
        await using (var entryStream = await archiveEntry.OpenEntryStreamAsync())
#endif
        {
            tarStream = new MemoryStream();
            await entryStream.CopyToAsync(tarStream);
        }
        Assert.Equal(size, tarStream.Length);
#if NETFRAMEWORK
        using (var entryStream = await archiveEntry.OpenEntryStreamAsync())
#else
        await using (var entryStream = await archiveEntry.OpenEntryStreamAsync())
#endif
        {
            var result = await TarArchive.IsTarFileAsync(entryStream);
            Assert.True(result);
        }
        Assert.Equal(size, tarStream.Length);
#if NETFRAMEWORK
        using (var entryStream = await archiveEntry.OpenEntryStreamAsync())
#else
        await using (var entryStream = await archiveEntry.OpenEntryStreamAsync())
#endif
        {
            tarStream = new MemoryStream();
            await entryStream.CopyToAsync(tarStream);
        }
        Assert.Equal(size, tarStream.Length);
    }

    [Fact]
    public async Task TestGzCrcWithMostSignificantBitNotNegative_Async()
    {
#if NETFRAMEWORK
        using var stream = File.OpenRead(Path.Combine(TEST_ARCHIVES_PATH, "Tar.tar.gz"));
#else
        await using var stream = File.OpenRead(Path.Combine(TEST_ARCHIVES_PATH, "Tar.tar.gz"));
#endif
        await using var archive = GZipArchive.OpenAsyncArchive(new AsyncOnlyStream(stream));
        await foreach (var entry in archive.EntriesAsync.Where(entry => !entry.IsDirectory))
        {
            Assert.InRange(entry.Crc, 0L, 0xFFFFFFFFL);
        }
    }

    [Fact]
    public async Task TestGzArchiveTypeGzip_Async()
    {
#if NETFRAMEWORK
        using var stream = File.OpenRead(Path.Combine(TEST_ARCHIVES_PATH, "Tar.tar.gz"));
#else
        await using var stream = File.OpenRead(Path.Combine(TEST_ARCHIVES_PATH, "Tar.tar.gz"));
#endif
        await using var archive = GZipArchive.OpenAsyncArchive(new AsyncOnlyStream(stream));
        Assert.Equal(archive.Type, ArchiveType.GZip);
    }
}
