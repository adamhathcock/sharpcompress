using System.IO;
using SharpCompress.Common;
using Xunit;

namespace SharpCompress.Test.GZip
{
    public class GZipReaderTests : ReaderTests
    {
        public GZipReaderTests()
        {
            UseExtensionInsteadOfNameToVerify = true;
        }

        [Fact]
        public void GZip_Reader_Generic()
        {
            Read("Tar.tar.gz", CompressionType.GZip);
        }
        
        
        [Fact]
        public void GZip_Reader_Generic2()
        {
            using (Stream stream = File.OpenRead(Path.Combine(TEST_ARCHIVES_PATH, "Tar.tar.gz")))
            using (SharpCompress.Readers.IReader reader = SharpCompress.Readers.GZip.GZipReader.Open(stream))
            {
                while (reader.MoveToNextEntry()) // Crash here
                { }
            }
        }
    }
}
