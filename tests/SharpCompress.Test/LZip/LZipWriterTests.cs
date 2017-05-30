using System.IO;
using SharpCompress.Common;
using SharpCompress.Writers;
using SharpCompress.Writers.LZip;
using Xunit;

namespace SharpCompress.Test
{
    public class LZipWriterTests : WriterTests
    {
        public LZipWriterTests()
            : base(ArchiveType.LZip)
        {
            UseExtensionInsteadOfNameToVerify = true;
        }

        [Fact]
        public void LZip_Writer_Generic()
        {
            ResetScratch();
            using (Stream stream = File.Open(Path.Combine(SCRATCH_FILES_PATH, "Tar.tar.lz"), FileMode.OpenOrCreate, FileAccess.Write))
            using (var writer = WriterFactory.Open(stream, ArchiveType.LZip, CompressionType.LZip))
            {
                writer.Write("Tar.tar", Path.Combine(TEST_ARCHIVES_PATH, "Tar.tar"));
            }
            CompareArchivesByPath(Path.Combine(SCRATCH_FILES_PATH, "Tar.tar.lz"),
                Path.Combine(TEST_ARCHIVES_PATH, "Tar.tar.lz"));
        }

        [Fact]
        public void LZip_Writer()
        {
            using (Stream stream = File.Open(Path.Combine(SCRATCH_FILES_PATH, "Tar.tar.lz"), FileMode.OpenOrCreate, FileAccess.Write))
            using (var writer = new LZipWriter(stream))
            {
                writer.Write("Tar.tar", Path.Combine(TEST_ARCHIVES_PATH, "Tar.tar"));
            }
        }

        [Fact]
        public void LZip_Writer_Generic_Bad_Compression()
        {
            Assert.Throws<InvalidFormatException>(() =>
                                                  {
                            ResetScratch();
                            using (Stream stream = File.OpenWrite(Path.Combine(SCRATCH_FILES_PATH, "Tar.tar.lz")))
                            using (var writer = WriterFactory.Open(stream, ArchiveType.LZip, CompressionType.BZip2))
                            {
                                writer.Write("Tar.tar", Path.Combine(TEST_ARCHIVES_PATH, "Tar.tar"));
                            }
                            CompareArchivesByPath(Path.Combine(SCRATCH_FILES_PATH, "Tar.tar.lz"),
                                Path.Combine(TEST_ARCHIVES_PATH, "Tar.tar.lz"));

                                                  });
        }
    }
}
