using System.IO;
using System.Threading.Tasks;
using SharpCompress.Common;
using SharpCompress.Writers;
using SharpCompress.Writers.GZip;
using Xunit;

namespace SharpCompress.Test.GZip
{
    public class GZipWriterTests : WriterTests
    {
        public GZipWriterTests()
            : base(ArchiveType.GZip)
        {
            UseExtensionInsteadOfNameToVerify = true;
        }

        [Fact]
        public async Task GZip_Writer_Generic()
        {
            await using (Stream stream = File.Open(Path.Combine(SCRATCH_FILES_PATH, "Tar.tar.gz"), FileMode.OpenOrCreate, FileAccess.Write))
            await using (var writer = WriterFactory.Open(stream, ArchiveType.GZip, CompressionType.GZip))
            {
                await writer.WriteAsync("Tar.tar", Path.Combine(TEST_ARCHIVES_PATH, "Tar.tar"));
            }
            await CompareArchivesByPathAsync(Path.Combine(SCRATCH_FILES_PATH, "Tar.tar.gz"),
                                       Path.Combine(TEST_ARCHIVES_PATH, "Tar.tar.gz"));
        }

        [Fact]
        public async Task GZip_Writer()
        {
            await using (Stream stream = File.Open(Path.Combine(SCRATCH_FILES_PATH, "Tar.tar.gz"), FileMode.OpenOrCreate, FileAccess.Write))
            await using (var writer = new GZipWriter(stream))
            {
                await writer.WriteAsync("Tar.tar", Path.Combine(TEST_ARCHIVES_PATH, "Tar.tar"));
            }
            await CompareArchivesByPathAsync(Path.Combine(SCRATCH_FILES_PATH, "Tar.tar.gz"),
                                       Path.Combine(TEST_ARCHIVES_PATH, "Tar.tar.gz"));
        }

        [Fact]
        public async Task GZip_Writer_Generic_Bad_Compression()
        {
            await Assert.ThrowsAsync<InvalidFormatException>(async () =>
            {
                await using (Stream stream = File.OpenWrite(Path.Combine(SCRATCH_FILES_PATH, "Tar.tar.gz")))
                await using (var writer = WriterFactory.Open(stream, ArchiveType.GZip, CompressionType.BZip2))
                {
                }
            });
        }

        [Fact]
        public async Task GZip_Writer_Entry_Path_With_Dir()
        {
            await using (Stream stream = File.Open(Path.Combine(SCRATCH_FILES_PATH, "Tar.tar.gz"), FileMode.OpenOrCreate, FileAccess.Write))
            await using (var writer = new GZipWriter(stream))
            {
                var path = Path.Combine(TEST_ARCHIVES_PATH, "Tar.tar");
                await writer.WriteAsync(path, path); //covers issue #532
            }
            await CompareArchivesByPathAsync(Path.Combine(SCRATCH_FILES_PATH, "Tar.tar.gz"),
                                       Path.Combine(TEST_ARCHIVES_PATH, "Tar.tar.gz"));
        }
    }
}
