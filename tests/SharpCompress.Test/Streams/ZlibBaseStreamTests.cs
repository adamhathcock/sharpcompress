using System.IO;
using SharpCompress.Compressors;
using SharpCompress.Compressors.Deflate;
using Xunit;

namespace SharpCompress.Test.Streams
{
    public class ZLibBaseStreamTests
    {
        [Fact]
        public void TestChunkedZlibCompressesEverything()
        {
            byte[] plainData = new byte[] { 0xf7, 0x1b, 0xda, 0x0f, 0xb6, 0x2b, 0x3d, 0x91, 0xd7, 0xe1, 0xb5, 0x11, 0x34, 0x5a, 0x51, 0x3f, 0x8b, 0xce, 0x49, 0xd2 };
            byte[] buf = new byte[plainData.Length * 2];

            MemoryStream plainStream1 = new MemoryStream(plainData);
            DeflateStream compressor1 = new DeflateStream(plainStream1, CompressionMode.Compress);
            // This is enough to read the entire data
            int realCompressedSize = compressor1.Read(buf, 0, plainData.Length * 2);

            MemoryStream plainStream2 = new MemoryStream(plainData);
            DeflateStream compressor2 = new DeflateStream(plainStream2, CompressionMode.Compress);
            int total = 0;
            int r = -1; // Jumpstart
            while (r != 0)
            {
                // Reading in chunks
                r = compressor2.Read(buf, 0, plainData.Length);
                total += r;
            }

            Assert.Equal(total, realCompressedSize);
        }
    }
}
