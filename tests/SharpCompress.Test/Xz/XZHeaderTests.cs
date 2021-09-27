using SharpCompress.Compressors.Xz;
using System.IO;
using Xunit;

namespace SharpCompress.Test.Xz
{
    public class XZHeaderTests : XZTestsBase
    {
        [Fact]
        public void ChecksMagicNumber()
        {
            var bytes = Compressed.Clone() as byte[];
            bytes[3]++;
            using (Stream badMagicNumberStream = new MemoryStream(bytes))
            {
                BinaryReader br = new BinaryReader(badMagicNumberStream);
                var header = new XZHeader(br);
                var ex = Assert.Throws<InvalidDataException>(() => { header.Process(); });
                Assert.Equal("Invalid XZ Stream", ex.Message);
            }
        }

        [Fact]
        public void CorruptHeaderThrows()
        {
            var bytes = Compressed.Clone() as byte[];
            bytes[8]++;
            using (Stream badCrcStream = new MemoryStream(bytes))
            {
                BinaryReader br = new BinaryReader(badCrcStream);
                var header = new XZHeader(br);
                var ex = Assert.Throws<InvalidDataException>(() => { header.Process(); });
                Assert.Equal("Stream header corrupt", ex.Message);
            }
        }

        [Fact]
        public void BadVersionIfCrcOkButStreamFlagUnknown()
        {
            var bytes = Compressed.Clone() as byte[];
            byte[] streamFlags = { 0x00, 0xF4 };
            byte[] crc = Crc32.Compute(streamFlags).ToLittleEndianBytes();
            streamFlags.CopyTo(bytes, 6);
            crc.CopyTo(bytes, 8);
            using (Stream badFlagStream = new MemoryStream(bytes))
            {
                BinaryReader br = new BinaryReader(badFlagStream);
                var header = new XZHeader(br);
                var ex = Assert.Throws<InvalidDataException>(() => { header.Process(); });
                Assert.Equal("Unknown XZ Stream Version", ex.Message);
            }
        }

        [Fact]
        public void ProcessesBlockCheckType()
        {
            BinaryReader br = new BinaryReader(CompressedStream);
            var header = new XZHeader(br);
            header.Process();
            Assert.Equal(CheckType.CRC64, header.BlockCheckType);
        }

        [Fact]
        public void CanCalculateBlockCheckSize()
        {
            BinaryReader br = new BinaryReader(CompressedStream);
            var header = new XZHeader(br);
            header.Process();
            Assert.Equal(8, header.BlockCheckSize);
        }

        [Fact]
        public void ProcessesStreamHeaderFromFactory()
        {
            var header = XZHeader.FromStream(CompressedStream);
            Assert.Equal(CheckType.CRC64, header.BlockCheckType);
        }
    }
}
