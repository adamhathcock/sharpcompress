using SharpCompress.Compressors.Xz;
using System.IO;
using Xunit;

namespace SharpCompress.Test.Xz
{
    public class XZIndexTests : XZTestsBase
    {
        protected override void RewindEmpty(Stream stream)
        {
            stream.Position = 12;
        }

        protected override void Rewind(Stream stream)
        {
            stream.Position = 356;
        }

        protected override void RewindIndexed(Stream stream)
        {
            stream.Position = 612;
        }

        [Fact]
        public void RecordsStreamStartOnInit()
        {
            using (Stream badStream = new MemoryStream(new byte[] { 1, 2, 3, 4, 5 }))
            {
                BinaryReader br = new BinaryReader(badStream);
                var index = new XZIndex(br, false);
                Assert.Equal(0, index.StreamStartPosition);
            }
        }

        [Fact]
        public void ThrowsIfHasNoIndexMarker()
        {
            using (Stream badStream = new MemoryStream(new byte[] { 1, 2, 3, 4, 5 }))
            {
                BinaryReader br = new BinaryReader(badStream);
                var index = new XZIndex(br, false);
                Assert.Throws<InvalidDataException>(() => index.Process());
            }
        }

        [Fact]
        public void ReadsNoRecord()
        {
            BinaryReader br = new BinaryReader(CompressedEmptyStream);
            var index = new XZIndex(br, false);
            index.Process();
            Assert.Equal((ulong)0, index.NumberOfRecords);
        }

        [Fact]
        public void ReadsOneRecord()
        {
            BinaryReader br = new BinaryReader(CompressedStream);
            var index = new XZIndex(br, false);
            index.Process();
            Assert.Equal((ulong)1, index.NumberOfRecords);
        }

        [Fact]
        public void ReadsMultipleRecords()
        {
            BinaryReader br = new BinaryReader(CompressedIndexedStream);
            var index = new XZIndex(br, false);
            index.Process();
            Assert.Equal((ulong)2, index.NumberOfRecords);
        }

        [Fact]
        public void ReadsFirstRecord()
        {
            BinaryReader br = new BinaryReader(CompressedStream);
            var index = new XZIndex(br, false);
            index.Process();
            Assert.Equal((ulong)OriginalBytes.Length, index.Records[0].UncompressedSize);
        }

        [Fact]
        public void SkipsPadding()
        {
            // Index with 3-byte padding.
            using (Stream badStream = new MemoryStream(new byte[] { 0x00, 0x01, 0x10, 0x80, 0x01, 0x00, 0x00, 0x00, 0xB1, 0x01, 0xD9, 0xC9, 0xFF }))
            {
                BinaryReader br = new BinaryReader(badStream);
                var index = new XZIndex(br, false);
                index.Process();
                Assert.Equal(0L, badStream.Position % 4L);
            }
        }
    }
}
