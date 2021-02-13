using System.Text;
using System.IO;
using System.Threading.Tasks;
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

        private async ValueTask<byte[]> ReadBytesAsync(XZBlock block, int bytesToRead)
        {
            byte[] buffer = new byte[bytesToRead];
            var read = await block.ReadAsync(buffer, 0, bytesToRead);
            if (read != bytesToRead)
            {
                throw new EndOfStreamException();
            }

            return buffer;
        }

        [Fact]
        public async Task OnFindIndexBlockThrow()
        {
            var bytes = new byte[] { 0 };
            await using (Stream indexBlockStream = new MemoryStream(bytes))
            {
                var XZBlock = new XZBlock(indexBlockStream, CheckType.CRC64, 8);
                await Assert.ThrowsAsync<XZIndexMarkerReachedException>(async () => { await ReadBytesAsync(XZBlock, 1); });
            }
        }

        [Fact]
        public async Task CrcIncorrectThrows()
        {
            var bytes = Compressed.Clone() as byte[];
            bytes[20]++;
            await using (Stream badCrcStream = new MemoryStream(bytes))
            {
                Rewind(badCrcStream);
                var XZBlock = new XZBlock(badCrcStream, CheckType.CRC64, 8);
                var ex = await Assert.ThrowsAsync<InvalidDataException>(async () => { await ReadBytesAsync(XZBlock, 1); });
                Assert.Equal("Block header corrupt", ex.Message);
            }
        }

        [Fact]
        public async Task CanReadM()
        {
            var XZBlock = new XZBlock(CompressedStream, CheckType.CRC64, 8);
            Assert.Equal(Encoding.ASCII.GetBytes("M"), await ReadBytesAsync(XZBlock, 1));
        }

        [Fact]
        public async Task CanReadMary()
        {
            var XZBlock = new XZBlock(CompressedStream, CheckType.CRC64, 8);
            Assert.Equal(Encoding.ASCII.GetBytes("M"), await ReadBytesAsync(XZBlock, 1));
            Assert.Equal(Encoding.ASCII.GetBytes("a"), await ReadBytesAsync(XZBlock, 1));
            Assert.Equal(Encoding.ASCII.GetBytes("ry"), await ReadBytesAsync(XZBlock, 2));
        }

        [Fact]
        public async Task CanReadPoemWithStreamReader()
        {
            var XZBlock = new XZBlock(CompressedStream, CheckType.CRC64, 8);
            var sr = new StreamReader(XZBlock);
            Assert.Equal(await sr.ReadToEndAsync(), Original);
        }

        [Fact]
        public async Task NoopWhenNoPadding()
        {
            // CompressedStream's only block has no padding.
            var XZBlock = new XZBlock(CompressedStream, CheckType.CRC64, 8);
            var sr = new StreamReader(XZBlock);
            await sr.ReadToEndAsync();
            Assert.Equal(0L, CompressedStream.Position % 4L);
        }

        [Fact]
        public async Task SkipsPaddingWhenPresent()
        {
            // CompressedIndexedStream's first block has 1-byte padding.
            var XZBlock = new XZBlock(CompressedIndexedStream, CheckType.CRC64, 8);
            var sr = new StreamReader(XZBlock);
            await sr.ReadToEndAsync();
            Assert.Equal(0L, CompressedIndexedStream.Position % 4L);
        }
    }
}
