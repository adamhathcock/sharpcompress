using System.IO;
using System.Threading;
using System.Threading.Tasks;
using SharpCompress.Compressors.LZMA;
using SharpCompress.Test.Mocks;
using Xunit;

namespace SharpCompress.Test.Streams;

public class LZipHeaderReadTests
{
    [Theory]
    [InlineData(12, 4096)]
    [InlineData(29, 536870912)]
    [InlineData(11, 0)]
    [InlineData(30, 0)]
    [InlineData(31, 0)]
    [InlineData(44, 0)]
    public async Task LZip_Header_Fragmentation_And_Dictionary_Validation(int encoded, int expected)
    {
        byte[] header = [0x4C, 0x5A, 0x49, 0x50, 1, (byte)encoded];
        using var sync = new ShortReadStream(new MemoryStream(header));
        using var asyncInput = new ShortReadStream(new MemoryStream(header));
        Assert.Equal(expected, LZipStream.ValidateAndReadSize(sync));
        Assert.Equal(
            expected,
            await LZipStream.ValidateAndReadSizeAsync(asyncInput, CancellationToken.None)
        );
    }
}
