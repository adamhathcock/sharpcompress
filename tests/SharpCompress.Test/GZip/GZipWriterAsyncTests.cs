using System.IO;
using System.Threading.Tasks;
using SharpCompress.Common;
using SharpCompress.Writers;
using SharpCompress.Writers.GZip;
using Xunit;

namespace SharpCompress.Test.GZip;

public class GZipWriterAsyncTests : WriterTests
{
    public GZipWriterAsyncTests()
        : base(ArchiveType.GZip) => UseExtensionInsteadOfNameToVerify = true;

    [Fact]
    public async Task GZip_Writer_Generic_Async()
    {
        using (
            Stream stream = File.Open(
                Path.Combine(SCRATCH_FILES_PATH, "Tar.tar.gz"),
                FileMode.OpenOrCreate,
                FileAccess.Write
            )
        )
        using (var writer = WriterFactory.Open(stream, ArchiveType.GZip, CompressionType.GZip))
        {
            await writer.WriteAsync("Tar.tar", Path.Combine(TEST_ARCHIVES_PATH, "Tar.tar"));
        }
        CompareArchivesByPath(
            Path.Combine(SCRATCH_FILES_PATH, "Tar.tar.gz"),
            Path.Combine(TEST_ARCHIVES_PATH, "Tar.tar.gz")
        );
    }

    [Fact]
    public async Task GZip_Writer_Async()
    {
        using (
            Stream stream = File.Open(
                Path.Combine(SCRATCH_FILES_PATH, "Tar.tar.gz"),
                FileMode.OpenOrCreate,
                FileAccess.Write
            )
        )
        using (var writer = new GZipWriter(stream))
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
            using var writer = WriterFactory.Open(stream, ArchiveType.GZip, CompressionType.BZip2);
        });

    [Fact]
    public async Task GZip_Writer_Entry_Path_With_Dir_Async()
    {
        using (
            Stream stream = File.Open(
                Path.Combine(SCRATCH_FILES_PATH, "Tar.tar.gz"),
                FileMode.OpenOrCreate,
                FileAccess.Write
            )
        )
        using (var writer = new GZipWriter(stream))
        {
            var path = Path.Combine(TEST_ARCHIVES_PATH, "Tar.tar");
            await writer.WriteAsync(path, path);
        }
        CompareArchivesByPath(
            Path.Combine(SCRATCH_FILES_PATH, "Tar.tar.gz"),
            Path.Combine(TEST_ARCHIVES_PATH, "Tar.tar.gz")
        );
    }
}
