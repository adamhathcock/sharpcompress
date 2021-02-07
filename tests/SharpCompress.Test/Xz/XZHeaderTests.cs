using SharpCompress.Compressors.Xz;
using System.IO;
using System.Threading.Tasks;
using Xunit;

namespace SharpCompress.Test.Xz
{
    public class XZHeaderTests : XZTestsBase
    {
        [Fact]
        public async ValueTask ChecksMagicNumber()
        {
            var bytes = Compressed.Clone() as byte[];
            bytes[3]++;
            await using (Stream badMagicNumberStream = new MemoryStream(bytes))
            {
                var header = new XZHeader(badMagicNumberStream);
                var ex = await Assert.ThrowsAsync<InvalidDataException>(async () => { await header.Process(); });
                Assert.Equal("Invalid XZ Stream", ex.Message);
            }
        }

        [Fact]
        public async ValueTask CorruptHeaderThrows()
        {
            var bytes = Compressed.Clone() as byte[];
            bytes[8]++;
            using (Stream badCrcStream = new MemoryStream(bytes))
            {
                var header = new XZHeader(badCrcStream);
                var ex = await Assert.ThrowsAsync<InvalidDataException>(async () => { await header.Process(); });
                Assert.Equal("Stream header corrupt", ex.Message);
            }
        }

        [Fact]
        public async ValueTask BadVersionIfCrcOkButStreamFlagUnknown()
        {
            var bytes = Compressed.Clone() as byte[];
            byte[] streamFlags = { 0x00, 0xF4 };
            byte[] crc = Crc32.Compute(streamFlags).ToLittleEndianBytes();
            streamFlags.CopyTo(bytes, 6);
            crc.CopyTo(bytes, 8);
            using (Stream badFlagStream = new MemoryStream(bytes))
            {
                var header = new XZHeader(badFlagStream);
                var ex = await Assert.ThrowsAsync<InvalidDataException>(async () => { await header.Process(); });
                Assert.Equal("Unknown XZ Stream Version", ex.Message);
            }
        }

        [Fact]
        public async ValueTask ProcessesBlockCheckType()
        {
            var header = new XZHeader(CompressedStream);
            await header.Process();
            Assert.Equal(CheckType.CRC64, header.BlockCheckType);
        }

        [Fact]
        public async ValueTask CanCalculateBlockCheckSize()
        {
            var header = new XZHeader(CompressedStream);
            await header.Process();
            Assert.Equal(8, header.BlockCheckSize);
        }

        [Fact]
        public async ValueTask ProcessesStreamHeaderFromFactory()
        {
            var header = await XZHeader.FromStream(CompressedStream);
            Assert.Equal(CheckType.CRC64, header.BlockCheckType);
        }
    }
}
