using System.IO;
using System.Text;
using System.Threading.Tasks;
using SharpCompress.Common;
using SharpCompress.Compressors.Xz;
using Xunit;

namespace SharpCompress.Test.Xz;

public class XzBlockAsyncTests : XzTestsBase
{
    protected override void Rewind(Stream stream) => stream.Position = 12;

    protected override void RewindIndexed(Stream stream) => stream.Position = 12;

    private static async Task<byte[]> ReadBytesAsync(XZBlock block, int bytesToRead)
    {
        var buffer = new byte[bytesToRead];
        var read = await block.ReadAsync(buffer, 0, bytesToRead).ConfigureAwait(false);
        if (read != bytesToRead)
        {
            throw new EndOfStreamException();
        }

        return buffer;
    }

    [Fact]
    public async Task OnFindIndexBlockThrowAsync()
    {
        var bytes = new byte[] { 0 };
        using Stream indexBlockStream = new MemoryStream(bytes);
        var xzBlock = new XZBlock(indexBlockStream, CheckType.CRC64, 8);
        await Assert.ThrowsAsync<XZIndexMarkerReachedException>(async () =>
        {
            await ReadBytesAsync(xzBlock, 1).ConfigureAwait(false);
        });
    }

    [Fact]
    public async Task CrcIncorrectThrowsAsync()
    {
        var bytes = (byte[])Compressed.Clone();
        bytes[20]++;
        using Stream badCrcStream = new MemoryStream(bytes);
        Rewind(badCrcStream);
        var xzBlock = new XZBlock(badCrcStream, CheckType.CRC64, 8);
        var ex = await Assert.ThrowsAsync<InvalidFormatException>(async () =>
        {
            await ReadBytesAsync(xzBlock, 1).ConfigureAwait(false);
        });
        Assert.Equal("Block header corrupt", ex.Message);
    }

    [Fact]
    public async Task CanReadMAsync()
    {
        var xzBlock = new XZBlock(CompressedStream, CheckType.CRC64, 8);
        Assert.Equal(
            Encoding.ASCII.GetBytes("M"),
            await ReadBytesAsync(xzBlock, 1).ConfigureAwait(false)
        );
    }

    [Fact]
    public async Task CanReadMaryAsync()
    {
        var xzBlock = new XZBlock(CompressedStream, CheckType.CRC64, 8);
        Assert.Equal(
            Encoding.ASCII.GetBytes("M"),
            await ReadBytesAsync(xzBlock, 1).ConfigureAwait(false)
        );
        Assert.Equal(
            Encoding.ASCII.GetBytes("a"),
            await ReadBytesAsync(xzBlock, 1).ConfigureAwait(false)
        );
        Assert.Equal(
            Encoding.ASCII.GetBytes("ry"),
            await ReadBytesAsync(xzBlock, 2).ConfigureAwait(false)
        );
    }

    [Fact]
    public async Task CanReadPoemWithStreamReaderAsync()
    {
        var xzBlock = new XZBlock(CompressedStream, CheckType.CRC64, 8);
        var sr = new StreamReader(xzBlock);
        Assert.Equal(await sr.ReadToEndAsync().ConfigureAwait(false), Original);
    }

    [Fact]
    public async Task NoopWhenNoPaddingAsync()
    {
        // CompressedStream's only block has no padding.
        var xzBlock = new XZBlock(CompressedStream, CheckType.CRC64, 8);
        var sr = new StreamReader(xzBlock);
        await sr.ReadToEndAsync().ConfigureAwait(false);
        Assert.Equal(0L, CompressedStream.Position % 4L);
    }

    [Fact]
    public async Task SkipsPaddingWhenPresentAsync()
    {
        // CompressedIndexedStream's first block has 1-byte padding.
        var xzBlock = new XZBlock(CompressedIndexedStream, CheckType.CRC64, 8);
        var sr = new StreamReader(xzBlock);
        await sr.ReadToEndAsync().ConfigureAwait(false);
        Assert.Equal(0L, CompressedIndexedStream.Position % 4L);
    }

    [Fact]
    public async Task HandlesPaddingInUnalignedBlockAsync()
    {
        var compressedUnaligned = new byte[Compressed.Length + 1];
        Compressed.CopyTo(compressedUnaligned, 1);
        var compressedUnalignedStream = new MemoryStream(compressedUnaligned);
        compressedUnalignedStream.Position = 13;

        // Compressed's only block has no padding.
        var xzBlock = new XZBlock(compressedUnalignedStream, CheckType.CRC64, 8);
        var sr = new StreamReader(xzBlock);
        await sr.ReadToEndAsync().ConfigureAwait(false);
        Assert.Equal(1L, compressedUnalignedStream.Position % 4L);
    }
}
