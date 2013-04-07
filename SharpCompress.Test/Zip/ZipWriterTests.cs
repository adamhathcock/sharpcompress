using Microsoft.VisualStudio.TestTools.UnitTesting;
using SharpCompress.Common;

namespace SharpCompress.Test
{
    [TestClass]
    public class ZipWriterTests : WriterTests
    {
        public ZipWriterTests()
            : base(ArchiveType.Zip)
        {
        }

        [TestMethod]
        public void Zip_Deflate_Write()
        {
            Write(CompressionType.Deflate, "Zip.deflate.noEmptyDirs.zip", "Zip.deflate.noEmptyDirs.zip");
        }


        [TestMethod]
        public void Zip_BZip2_Write()
        {
            Write(CompressionType.BZip2, "Zip.bzip2.noEmptyDirs.zip", "Zip.bzip2.noEmptyDirs.zip");
        }


        [TestMethod]
        public void Zip_None_Write()
        {
            Write(CompressionType.None, "Zip.none.noEmptyDirs.zip", "Zip.none.noEmptyDirs.zip");
        }


        [TestMethod]
        public void Zip_LZMA_Write()
        {
            Write(CompressionType.LZMA, "Zip.lzma.noEmptyDirs.zip", "Zip.lzma.noEmptyDirs.zip");
        }

        [TestMethod]
        public void Zip_PPMd_Write()
        {
            Write(CompressionType.PPMd, "Zip.ppmd.noEmptyDirs.zip", "Zip.ppmd.noEmptyDirs.zip");
        }

        [TestMethod]
        [ExpectedException(typeof(InvalidFormatException))]
        public void Zip_Rar_Write()
        {
            Write(CompressionType.Rar, "Zip.ppmd.noEmptyDirs.zip", "Zip.ppmd.noEmptyDirs.zip");
        }
    }
}
