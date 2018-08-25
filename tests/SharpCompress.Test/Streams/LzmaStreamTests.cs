using System.IO;
using SharpCompress.Compressors.LZMA;
using Xunit;

namespace SharpCompress.Test.Streams
{
    public class LzmaStreamTests
    {
        [Fact]
        public void TestLzma2Decompress1Byte()
        {
            byte[] properties = new byte[] { 0x01 };
            byte[] compressedData = new byte[] { 0x01, 0x00, 0x00, 0x58, 0x00 };
            MemoryStream lzma2Stream = new MemoryStream(compressedData);

            LzmaStream decompressor = new LzmaStream(properties, lzma2Stream, 5, 1);
            Assert.Equal('X', decompressor.ReadByte());
        }
    }
}