using System.Text;

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
        public void Zip_Deflate_Write()
        {
            Write(CompressionType.Deflate, "Zip.deflate.noEmptyDirs.zip", "Zip.deflate.noEmptyDirs.zip", Encoding.UTF8);
        }

        [Fact]
        public void Zip_BZip2_Write()
        {
            Write(CompressionType.BZip2, "Zip.bzip2.noEmptyDirs.zip", "Zip.bzip2.noEmptyDirs.zip", Encoding.UTF8);
        }

        [Fact]
        public void Zip_None_Write()
        {
            Write(CompressionType.None, "Zip.none.noEmptyDirs.zip", "Zip.none.noEmptyDirs.zip", Encoding.UTF8);
        }

        [Fact]
        public void Zip_LZMA_Write()
        {
            Write(CompressionType.LZMA, "Zip.lzma.noEmptyDirs.zip", "Zip.lzma.noEmptyDirs.zip", Encoding.UTF8);
        }

        [Fact]
        public void Zip_PPMd_Write()
        {
            Write(CompressionType.PPMd, "Zip.ppmd.noEmptyDirs.zip", "Zip.ppmd.noEmptyDirs.zip", Encoding.UTF8);
        }


        [Fact]
        public void Zip_Rar_Write()
        {
            Assert.Throws<InvalidFormatException>(() => Write(CompressionType.Rar, "Zip.ppmd.noEmptyDirs.zip", "Zip.ppmd.noEmptyDirs.zip"));
        }
    }
}
