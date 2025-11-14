using System.IO;
using System.Threading.Tasks;
using SharpCompress.Common;
using SharpCompress.Compressors.Xz;
using Xunit;

namespace SharpCompress.Test.Xz;

public class XzHeaderAsyncTests : XzTestsBase
{
    [Fact]
    public async Task ChecksMagicNumberAsync()
    {
        var bytes = (byte[])Compressed.Clone();
        bytes[3]++;
        using Stream badMagicNumberStream = new MemoryStream(bytes);
        var br = new BinaryReader(badMagicNumberStream);
        var header = new XZHeader(br);
        var ex = await Assert.ThrowsAsync<InvalidFormatException>(async () =>
        {
            await header.ProcessAsync().ConfigureAwait(false);
        });
        Assert.Equal("Invalid XZ Stream", ex.Message);
    }

    [Fact]
    public async Task CorruptHeaderThrowsAsync()
    {
        var bytes = (byte[])Compressed.Clone();
        bytes[8]++;
        using Stream badCrcStream = new MemoryStream(bytes);
        var br = new BinaryReader(badCrcStream);
        var header = new XZHeader(br);
        var ex = await Assert.ThrowsAsync<InvalidFormatException>(async () =>
        {
            await header.ProcessAsync().ConfigureAwait(false);
        });
        Assert.Equal("Stream header corrupt", ex.Message);
    }

    [Fact]
    public async Task BadVersionIfCrcOkButStreamFlagUnknownAsync()
    {
        var bytes = (byte[])Compressed.Clone();
        byte[] streamFlags = [0x00, 0xF4];
        var crc = Crc32.Compute(streamFlags).ToLittleEndianBytes();
        streamFlags.CopyTo(bytes, 6);
        crc.CopyTo(bytes, 8);
        using Stream badFlagStream = new MemoryStream(bytes);
        var br = new BinaryReader(badFlagStream);
        var header = new XZHeader(br);
        var ex = await Assert.ThrowsAsync<InvalidFormatException>(async () =>
        {
            await header.ProcessAsync().ConfigureAwait(false);
        });
        Assert.Equal("Unknown XZ Stream Version", ex.Message);
    }

    [Fact]
    public async Task ProcessesBlockCheckTypeAsync()
    {
        var br = new BinaryReader(CompressedStream);
        var header = new XZHeader(br);
        await header.ProcessAsync().ConfigureAwait(false);
        Assert.Equal(CheckType.CRC64, header.BlockCheckType);
    }

    [Fact]
    public async Task CanCalculateBlockCheckSizeAsync()
    {
        var br = new BinaryReader(CompressedStream);
        var header = new XZHeader(br);
        await header.ProcessAsync().ConfigureAwait(false);
        Assert.Equal(8, header.BlockCheckSize);
    }

    [Fact]
    public async Task ProcessesStreamHeaderFromFactoryAsync()
    {
        var header = await XZHeader.FromStreamAsync(CompressedStream).ConfigureAwait(false);
        Assert.Equal(CheckType.CRC64, header.BlockCheckType);
    }
}
