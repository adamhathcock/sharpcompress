using SharpCompress.Common;
using Xunit;

namespace SharpCompress.Test
{
    public class TarWriterTests : WriterTests
    {
        public TarWriterTests()
            : base(ArchiveType.Tar)
        {
            UseExtensionInsteadOfNameToVerify = true;
        }

        [Fact]
        public void Tar_Writer()
        {
            Write(CompressionType.None, "Tar.noEmptyDirs.tar", "Tar.noEmptyDirs.tar");
        }

        [Fact]
        public void Tar_BZip2_Writer()
        {
            Write(CompressionType.BZip2, "Tar.noEmptyDirs.tar.bz2", "Tar.noEmptyDirs.tar.bz2");
        }

        [Fact]
        [ExpectedException(typeof(InvalidFormatException))]
        public void Tar_Rar_Write()
        {
            Write(CompressionType.Rar, "Zip.ppmd.noEmptyDirs.zip", "Zip.ppmd.noEmptyDirs.zip");
        }
    }
}
