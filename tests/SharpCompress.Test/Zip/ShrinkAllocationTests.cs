using System.Buffers;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using SharpCompress.Common;
using SharpCompress.Compressors.Shrink;
using Xunit;

namespace SharpCompress.Test.Zip;

public class ShrinkAllocationTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Shrink_Rejects_Huge_Truncated_Output(bool useAsync)
    {
        using var input = new MemoryStream(new byte[] { 65, 0 });
        if (useAsync)
        {
            await Assert.ThrowsAsync<InvalidFormatException>(async () =>
            {
                using var stream = await ShrinkStream.CreateAsync(
                    input,
                    int.MaxValue,
                    CancellationToken.None
                );
            });
        }
        else
        {
            using var stream = new ShrinkStream(input, int.MaxValue);
            Assert.Throws<InvalidFormatException>(() => stream.ReadByte());
        }
    }

    [Fact]
    public void Shrink_Grows_Only_For_Actual_Output()
    {
        var pool = ArrayPool<byte>.Shared;
        var status = HwUnshrink.UnshrinkGrowing(
            new byte[] { 65, 132, 0 },
            3,
            int.MaxValue,
            pool,
            out var output,
            out var used
        );
        try
        {
            Assert.Equal(HwUnshrink.UnshrnkStatus.Ok, status);
            Assert.Equal(2, used);
            Assert.Equal(65, output[0]);
            Assert.Equal(66, output[1]);
            Assert.InRange(output.Length, 2, 131072);
        }
        finally
        {
            pool.Return(output);
        }
    }

    [Fact]
    public void Shrink_Preserves_Fixed_Buffer_API()
    {
        var output = new byte[2];
        Assert.Equal(
            HwUnshrink.UnshrnkStatus.Ok,
            HwUnshrink.Unshrink(new byte[] { 65, 132, 0 }, 3, out _, output, 2, out var used)
        );
        Assert.Equal(2, used);
        Assert.Equal(new byte[] { 65, 66 }, output);
    }
}
