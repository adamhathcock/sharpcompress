using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using SharpCompress.Archives.Zip;
using SharpCompress.Readers;
using SharpCompress.Readers.Zip;
using SharpCompress.Test.Mocks;
using Xunit;

namespace SharpCompress.Test.Zip;

public class ZipAesShortReadTests : TestBase
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Zip_Aes_Archive_Reads_Fragmented_Metadata(bool async)
    {
        using var input = new ShortReadStream(
            File.OpenRead(Path.Combine(TEST_ARCHIVES_PATH, "Zip.deflate.WinzipAES.zip"))
        );
        using var output = new MemoryStream();
        var options = new ReaderOptions { Password = "test", LeaveStreamOpen = true };
        if (async)
        {
            await using var archive = await ZipArchive.OpenAsyncArchive(
                input,
                options,
                CancellationToken.None
            );
            await foreach (var entry in archive.EntriesAsync)
            {
                if (entry.IsDirectory)
                {
                    continue;
                }
                using var entryStream = await entry.OpenEntryStreamAsync(CancellationToken.None);
                await entryStream.CopyToAsync(output, 81920, CancellationToken.None);
            }
        }
        else
        {
            using var archive = ZipArchive.OpenArchive(input, options);
            foreach (var entry in archive.Entries.Where(e => !e.IsDirectory))
            {
                using var entryStream = entry.OpenEntryStream();
                entryStream.CopyTo(output);
            }
        }
        Assert.True(output.Length > 0);
        Assert.False(input.IsDisposed);
    }

    [Fact]
    public void Zip_Aes_Reader_Reads_Fragmented_Metadata()
    {
        using var input = new ShortReadStream(
            File.OpenRead(Path.Combine(TEST_ARCHIVES_PATH, "Zip.deflate.WinzipAES.zip")),
            seekable: false
        );
        using var reader = ZipReader.OpenReader(
            input,
            new ReaderOptions { Password = "test", LeaveStreamOpen = true }
        );
        using var output = new MemoryStream();
        while (reader.MoveToNextEntry())
        {
            if (reader.Entry.IsDirectory)
            {
                continue;
            }
            using var entryStream = reader.OpenEntryStream();
            entryStream.CopyTo(output);
        }
        Assert.True(output.Length > 0);
    }
}
