using System.IO;
using System.Threading.Tasks;
using SharpCompress.Common;
using SharpCompress.Providers;
using SharpCompress.Providers.System;
using SharpCompress.Test.Mocks;
using SharpCompress.Writers;
using SharpCompress.Writers.GZip;
using Xunit;

namespace SharpCompress.Test.GZip;

public class GZipWriterAsyncTests : WriterTests
{
    public GZipWriterAsyncTests()
        : base(ArchiveType.GZip) => UseExtensionInsteadOfNameToVerify = true;

    [Fact]
    public async ValueTask GZip_Writer_Generic_Async()
    {
        using (
            Stream stream = File.Open(
                Path.Combine(SCRATCH_FILES_PATH, "Tar.tar.gz"),
                FileMode.OpenOrCreate,
                FileAccess.Write
            )
        )
        await using (
            var writer = await WriterFactory.OpenAsyncWriter(
                new AsyncOnlyStream(stream),
                ArchiveType.GZip,
                new WriterOptions(CompressionType.GZip)
            )
        )
        {
            await writer.WriteAsync("Tar.tar", Path.Combine(TEST_ARCHIVES_PATH, "Tar.tar"));
        }
        CompareArchivesByPath(
            Path.Combine(SCRATCH_FILES_PATH, "Tar.tar.gz"),
            Path.Combine(TEST_ARCHIVES_PATH, "Tar.tar.gz")
        );
    }

    [Fact]
    public async ValueTask GZip_Writer_Async()
    {
        using (
            Stream stream = File.Open(
                Path.Combine(SCRATCH_FILES_PATH, "Tar.tar.gz"),
                FileMode.OpenOrCreate,
                FileAccess.Write
            )
        )
        await using (var writer = new GZipWriter(new AsyncOnlyStream(stream)))
        {
            await writer.WriteAsync("Tar.tar", Path.Combine(TEST_ARCHIVES_PATH, "Tar.tar"));
        }
        CompareArchivesByPath(
            Path.Combine(SCRATCH_FILES_PATH, "Tar.tar.gz"),
            Path.Combine(TEST_ARCHIVES_PATH, "Tar.tar.gz")
        );
    }

    [Fact]
    public void GZip_Writer_Generic_Bad_Compression_Async() =>
        Assert.Throws<InvalidFormatException>(() =>
        {
            using Stream stream = File.OpenWrite(Path.Combine(SCRATCH_FILES_PATH, "Tar.tar.gz"));
            using var writer = WriterFactory.OpenWriter(
                new AsyncOnlyStream(stream),
                ArchiveType.GZip,
                new WriterOptions(CompressionType.BZip2)
            );
        });

    [Fact]
    public async ValueTask GZip_Writer_Entry_Path_With_Dir_Async()
    {
        using (
            Stream stream = File.Open(
                Path.Combine(SCRATCH_FILES_PATH, "Tar.tar.gz"),
                FileMode.OpenOrCreate,
                FileAccess.Write
            )
        )
        await using (var writer = new GZipWriter(new AsyncOnlyStream(stream)))
        {
            var path = Path.Combine(TEST_ARCHIVES_PATH, "Tar.tar");
            await writer.WriteAsync(path, path);
        }
        CompareArchivesByPath(
            Path.Combine(SCRATCH_FILES_PATH, "Tar.tar.gz"),
            Path.Combine(TEST_ARCHIVES_PATH, "Tar.tar.gz")
        );
    }

    [Fact]
    public async ValueTask GZip_Writer_Async_With_System_Provider()
    {
        var contents = new byte[] { 1, 2, 3, 4 };
        var providers = CompressionProviderRegistry.Default.With(
            new SystemGZipCompressionProvider()
        );
        using var source = new MemoryStream(contents);
        using var destination = new MemoryStream();
        await using (
            var writer = new GZipWriter(
                destination,
                new GZipWriterOptions { LeaveStreamOpen = true, Providers = providers }
            )
        )
        {
            await writer.WriteAsync("contents.bin", source);
        }

        destination.Position = 0;
        using var compressed = new SystemGZipCompressionProvider().CreateDecompressStream(
            destination
        );
        using var actual = new MemoryStream();
        compressed.CopyTo(actual);

        Assert.Equal(contents, actual.ToArray());
    }
}
