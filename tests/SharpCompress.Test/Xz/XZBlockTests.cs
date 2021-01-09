using System.Text;
using System.IO;
using SharpCompress.Compressors.Xz;
using Xunit;

namespace SharpCompress.Test.Xz
{
    public class XZBlockTests : XZTestsBase
    {
        protected override void Rewind(Stream stream)
        {
            stream.Position = 12;
        }

        protected override void RewindIndexed(Stream stream)
        {
            stream.Position = 12;
        }

        private byte[] ReadBytes(XZBlock block, int bytesToRead)
        {
            byte[] buffer = new byte[bytesToRead];
            var read = block.Read(buffer, 0, bytesToRead);
            if (read != bytesToRead)
            {
                throw new EndOfStreamException();
            }

            return buffer;
        }

        [Fact]
        public void OnFindIndexBlockThrow()
        {
            var bytes = new byte[] { 0 };
            using (Stream indexBlockStream = new MemoryStream(bytes))
            {
                var XZBlock = new XZBlock(indexBlockStream, CheckType.CRC64, 8);
                Assert.Throws<XZIndexMarkerReachedException>(() => { ReadBytes(XZBlock, 1); });
            }
        }

        [Fact]
        public void CrcIncorrectThrows()
        {
            var bytes = Compressed.Clone() as byte[];
            bytes[20]++;
            using (Stream badCrcStream = new MemoryStream(bytes))
            {
                Rewind(badCrcStream);
                var XZBlock = new XZBlock(badCrcStream, CheckType.CRC64, 8);
                var ex = Assert.Throws<InvalidDataException>(() => { ReadBytes(XZBlock, 1); });
                Assert.Equal("Block header corrupt", ex.Message);
            }
        }

        [Fact]
        public void CanReadM()
        {
            var XZBlock = new XZBlock(CompressedStream, CheckType.CRC64, 8);
            Assert.Equal(Encoding.ASCII.GetBytes("M"), ReadBytes(XZBlock, 1));
        }

        [Fact]
        public void CanReadMary()
        {
            var XZBlock = new XZBlock(CompressedStream, CheckType.CRC64, 8);
            Assert.Equal(Encoding.ASCII.GetBytes("M"), ReadBytes(XZBlock, 1));
            Assert.Equal(Encoding.ASCII.GetBytes("a"), ReadBytes(XZBlock, 1));
            Assert.Equal(Encoding.ASCII.GetBytes("ry"), ReadBytes(XZBlock, 2));
        }

        [Fact]
        public void CanReadPoemWithStreamReader()
        {
            var XZBlock = new XZBlock(CompressedStream, CheckType.CRC64, 8);
            var sr = new StreamReader(XZBlock);
            Assert.Equal(sr.ReadToEnd(), Original);
        }

        [Fact]
        public void NoopWhenNoPadding()
        {
            // CompressedStream's only block has no padding.
            var XZBlock = new XZBlock(CompressedStream, CheckType.CRC64, 8);
            var sr = new StreamReader(XZBlock);
            sr.ReadToEnd();
            Assert.Equal(0L, CompressedStream.Position % 4L);
        }

        [Fact]
        public void SkipsPaddingWhenPresent()
        {
            // CompressedIndexedStream's first block has 1-byte padding.
            var XZBlock = new XZBlock(CompressedIndexedStream, CheckType.CRC64, 8);
            var sr = new StreamReader(XZBlock);
            sr.ReadToEnd();
            Assert.Equal(0L, CompressedIndexedStream.Position % 4L);
        }
    }
}
