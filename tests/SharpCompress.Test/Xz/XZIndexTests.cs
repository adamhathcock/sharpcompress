using SharpCompress.Compressors.Xz;
using System.IO;
using Xunit;

namespace SharpCompress.Test.Xz
{
    public class XZIndexTests : XZTestsBase
    {
        protected override void Rewind(Stream stream)
        {
            stream.Position = 356;
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
                Assert.Throws<InvalidDataException>( () => index.Process());
            }
        }

        [Fact]
        public void ReadsNumberOfRecords()
        {
            BinaryReader br = new BinaryReader(CompressedStream);
            var index = new XZIndex(br, false);
            index.Process();
            Assert.Equal(index.NumberOfRecords, (ulong)1);
        }

        [Fact]
        public void ReadsFirstRecord()
        {
            BinaryReader br = new BinaryReader(CompressedStream);
            var index = new XZIndex(br, false);
            index.Process();
            Assert.Equal((ulong)OriginalBytes.Length, index.Records[0].UncompressedSize);
        }
    }
}
