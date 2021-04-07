using System.Text;
using System.Threading.Tasks;
using SharpCompress.Common;
using Xunit;

namespace SharpCompress.Test.Zip
{
    public class ZipWriterTests : WriterTests
    {
        public ZipWriterTests()
            : base(ArchiveType.Zip)
        {
        }


        [Fact]
        public async Task Zip_Deflate_Write()
        {
            await WriteAsync(CompressionType.Deflate, "Zip.deflate.noEmptyDirs.zip", "Zip.deflate.noEmptyDirs.zip", Encoding.UTF8);
        }

        [Fact]
        public async Task Zip_BZip2_Write()
        {
            await WriteAsync(CompressionType.BZip2, "Zip.bzip2.noEmptyDirs.zip", "Zip.bzip2.noEmptyDirs.zip", Encoding.UTF8);
        }

        [Fact]
        public async Task Zip_None_Write()
        {
            await WriteAsync(CompressionType.None, "Zip.none.noEmptyDirs.zip", "Zip.none.noEmptyDirs.zip", Encoding.UTF8);
        }

        [Fact]
        public async Task Zip_LZMA_Write()
        {
            await WriteAsync(CompressionType.LZMA, "Zip.lzma.noEmptyDirs.zip", "Zip.lzma.noEmptyDirs.zip", Encoding.UTF8);
        }

        [Fact]
        public async Task Zip_PPMd_Write()
        {
            await WriteAsync(CompressionType.PPMd, "Zip.ppmd.noEmptyDirs.zip", "Zip.ppmd.noEmptyDirs.zip", Encoding.UTF8);
        }


        [Fact]
        public async Task Zip_Rar_Write()
        {
            await Assert.ThrowsAsync<InvalidFormatException>(async () => await WriteAsync(CompressionType.Rar, "Zip.ppmd.noEmptyDirs.zip", "Zip.ppmd.noEmptyDirs.zip"));
        }
    }
}
