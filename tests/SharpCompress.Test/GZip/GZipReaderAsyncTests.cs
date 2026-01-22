using System.IO;
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
        await using var reader = GZipReader.OpenAsyncReader(new AsyncOnlyStream(stream));
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
}
