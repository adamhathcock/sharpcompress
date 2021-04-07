using System.IO;
using System.Text;
using System.Threading.Tasks;
using SharpCompress.Common;
using SharpCompress.Writers.Tar;
using Xunit;

namespace SharpCompress.Test.Tar
{
    public class TarWriterTests : WriterTests
    {
        public TarWriterTests()
            : base(ArchiveType.Tar)
        {
            UseExtensionInsteadOfNameToVerify = true;
        }

        [Fact]
        public async Task Tar_Writer()
        {
            await WriteAsync(CompressionType.None, "Tar.noEmptyDirs.tar", "Tar.noEmptyDirs.tar", Encoding.GetEncoding(866));
        }

        [Fact]
        public async Task Tar_BZip2_Writer()
        {
            await WriteAsync(CompressionType.BZip2, "Tar.noEmptyDirs.tar.bz2", "Tar.noEmptyDirs.tar.bz2", Encoding.GetEncoding(866));
        }

        [Fact]
        public async Task Tar_LZip_Writer()
        {
            await WriteAsync(CompressionType.LZip, "Tar.noEmptyDirs.tar.lz", "Tar.noEmptyDirs.tar.lz", Encoding.GetEncoding(866));
        }

        [Fact]
        public async Task Tar_Rar_Write()
        {
            await Assert.ThrowsAsync<InvalidFormatException>(async () => await WriteAsync(CompressionType.Rar, "Zip.ppmd.noEmptyDirs.zip", "Zip.ppmd.noEmptyDirs.zip"));
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public async Task Tar_Finalize_Archive(bool finalizeArchive)
        {
            await using (MemoryStream stream = new MemoryStream())
            await using (Stream content = File.OpenRead(Path.Combine(ORIGINAL_FILES_PATH, "jpg", "test.jpg")))
            {
                await using (TarWriter writer = await TarWriter.CreateAsync(stream, new TarWriterOptions(CompressionType.None, finalizeArchive)))
                {
                    await writer.WriteAsync("doesn't matter", content, null);
                }

                var paddedContentWithHeader = content.Length / 512 * 512 + 512 + 512;
                var expectedStreamLength = finalizeArchive ? paddedContentWithHeader + 512 * 2 : paddedContentWithHeader;
                Assert.Equal(expectedStreamLength, stream.Length);
            }
        }
    }
}
