using System.Text;

using SharpCompress.Common;
using Xunit;
using System.IO;
using SharpCompress.Writers.Zip;
using SharpCompress.Compressors.Deflate;

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

        [Fact]
        public void Zip_Write_MemoryStream()
        {
            var ms = new MemoryStream();
            var zw = new ZipWriter(ms, new ZipWriterOptions(compressionType: CompressionType.Deflate ) { DeflateCompressionLevel = CompressionLevel.None } );
            var payload = new string('\n', 100000);
            using (var stream = zw.WriteToStream("test.txt", new ZipWriterEntryOptions()))
                using (var streamWriter = new StreamWriter(stream: stream))
            {
                streamWriter.Write(payload);
            }

            using( var file = new FileStream("d:\\projects\\test.zip", FileMode.Create, FileAccess.Write))
            {
                ms.WriteTo(file);
            }
        }
    }
}
