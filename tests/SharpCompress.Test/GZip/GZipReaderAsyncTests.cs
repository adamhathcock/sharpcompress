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
    public async Task GZip_Reader_Generic_Async() =>
        await ReadAsync("Tar.tar.gz", CompressionType.GZip);

    [Fact]
    public async Task GZip_Reader_Generic2_Async()
    {
        //read only as GZip item
        using Stream stream = File.OpenRead(Path.Combine(TEST_ARCHIVES_PATH, "Tar.tar.gz"));
        using var reader = GZipReader.Open(new SharpCompressStream(stream));
        while (reader.MoveToNextEntry())
        {
            Assert.NotEqual(0, reader.Entry.Size);
            Assert.NotEqual(0, reader.Entry.Crc);

            // Use async overload for reading the entry
            if (!reader.Entry.IsDirectory)
            {
                using var entryStream = reader.OpenEntryStream();
                using var ms = new MemoryStream();
                await entryStream.CopyToAsync(ms);
            }
        }
    }

    protected async Task ReadAsync(
        string testArchive,
        CompressionType expectedCompression,
        ReaderOptions? options = null
    )
    {
        testArchive = Path.Combine(TEST_ARCHIVES_PATH, testArchive);

        options ??= new ReaderOptions() { BufferSize = 0x20000 };

        options.LeaveStreamOpen = true;
        await ReadImplAsync(testArchive, expectedCompression, options);

        options.LeaveStreamOpen = false;
        await ReadImplAsync(testArchive, expectedCompression, options);
        VerifyFiles();
    }

    private async Task ReadImplAsync(
        string testArchive,
        CompressionType expectedCompression,
        ReaderOptions options
    )
    {
        using var file = File.OpenRead(testArchive);
        using var protectedStream = SharpCompressStream.Create(
            new ForwardOnlyStream(file, options.BufferSize),
            leaveOpen: true,
            throwOnDispose: true,
            bufferSize: options.BufferSize
        );
        using var testStream = new TestStream(protectedStream);
        using (var reader = ReaderFactory.Open(testStream, options))
        {
            await UseReaderAsync(reader, expectedCompression);
            protectedStream.ThrowOnDispose = false;
            Assert.False(testStream.IsDisposed, $"{nameof(testStream)} prematurely closed");
        }

        var message =
            $"{nameof(options.LeaveStreamOpen)} is set to '{options.LeaveStreamOpen}', so {nameof(testStream.IsDisposed)} should be set to '{!testStream.IsDisposed}', but is set to {testStream.IsDisposed}";
        Assert.True(options.LeaveStreamOpen != testStream.IsDisposed, message);
    }

    private async Task UseReaderAsync(IReader reader, CompressionType expectedCompression)
    {
        while (reader.MoveToNextEntry())
        {
            if (!reader.Entry.IsDirectory)
            {
                Assert.Equal(expectedCompression, reader.Entry.CompressionType);
                await reader.WriteEntryToDirectoryAsync(
                    SCRATCH_FILES_PATH,
                    new ExtractionOptions { ExtractFullPath = true, Overwrite = true }
                );
            }
        }
    }
}
