using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SharpCompress.Compressor.LZMA;

namespace SharpCompress.Test.Streams
{
    [TestClass]
    public class StreamTests
    {
        [TestMethod]
        public void TestLzma2Decompress1Byte()
        {
            byte[] properties = new byte[] { 0x01 };
            byte[] compressedData = new byte[] { 0x01, 0x00, 0x00, 0x58, 0x00 };
            MemoryStream lzma2Stream = new MemoryStream(compressedData);

            LzmaStream decompressor = new LzmaStream(properties, lzma2Stream, 5, 1);
            Assert.AreEqual('X', decompressor.ReadByte());
        }
    }
}