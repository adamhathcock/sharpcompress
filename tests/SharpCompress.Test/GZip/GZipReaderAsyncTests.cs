using System.IO;
using System.IO.Compression;
using System.Threading.Tasks;
using SharpCompress.Common;
using SharpCompress.IO;
using SharpCompress.Readers;
using SharpCompress.Readers.GZip;
using SharpCompress.Test.Mocks;
using Xunit;

namespace SharpCompress.Test.GZip;

public class GZipReaderAsyncTests : ReaderTests
{
    public GZipReaderAsyncTests() => UseExtensionInsteadOfNameToVerify = true;

    [Fact]
    public async ValueTask GZip_Reader_Generic_Async() =>
        await ReadAsync("Tar.tar.gz", CompressionType.GZip);

    [Fact]
    public async ValueTask GZip_Reader_Generic2_Async()
    {
        //read only as GZip item
        using Stream stream = File.OpenRead(Path.Combine(TEST_ARCHIVES_PATH, "Tar.tar.gz"));
        await using var reader = await GZipReader.OpenAsyncReader(new AsyncOnlyStream(stream));
        while (await reader.MoveToNextEntryAsync())
        {
            Assert.NotEqual(0, reader.Entry.Size);
            Assert.NotEqual(0, reader.Entry.Crc);

            // Use async overload for reading the entry
            if (!reader.Entry.IsDirectory)
            {
                using var entryStream = await reader.OpenEntryStreamAsync();
                using var ms = new MemoryStream();
                await entryStream.CopyToAsync(ms);
            }
        }
    }

    [Fact]
    public async ValueTask GZip_ReaderFactory_FlatGZip_Async()
    {
        var source = new byte[2048];
        for (var i = 0; i < source.Length; i++)
        {
            source[i] = 0xFF;
        }

        var gzipPath = Path.Combine(SCRATCH_FILES_PATH, "Flat.bin.gz");
        using (var output = File.Create(gzipPath))
        using (var gzip = new GZipStream(output, CompressionMode.Compress))
        {
            await gzip.WriteAsync(source, 0, source.Length);
        }

        using Stream stream = File.OpenRead(gzipPath);
        await using var reader = await ReaderFactory.OpenAsyncReader(stream);
        Assert.IsType<GZipReader>(reader);
        Assert.True(await reader.MoveToNextEntryAsync());

        using var ms = new MemoryStream();
        await reader.WriteEntryToAsync(ms);
        Assert.Equal(source.Length, ms.Length);
        Assert.Equal(source, ms.ToArray());

        Assert.False(await reader.MoveToNextEntryAsync());
    }
}
