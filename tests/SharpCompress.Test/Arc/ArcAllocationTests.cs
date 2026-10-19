using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using SharpCompress.Common;
using SharpCompress.Compressors.ArcLzw;
using Xunit;

namespace SharpCompress.Test.Arc;

public class ArcAllocationTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Arc_Rejects_Huge_Truncated_Compressed_Data(bool useAsync)
    {
        using var input = new MemoryStream(new byte[] { 12, 65, 0 });
        using var stream = new ArcLzwStream(input, int.MaxValue);
        var buffer = new byte[1];
        if (useAsync)
        {
            await Assert.ThrowsAsync<IncompleteArchiveException>(() =>
                stream.ReadAsync(buffer, 0, 1, CancellationToken.None)
            );
        }
        else
        {
            Assert.Throws<IncompleteArchiveException>(() => stream.Read(buffer, 0, 1));
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Arc_Preserves_Output_Across_Small_Reads(bool useAsync)
    {
        // Two nine-bit literals, followed by a byte belonging to the next entry.
        using var input = new MemoryStream(new byte[] { 65, 132, 0, 99 });
        using var stream = new ArcLzwStream(input, 3, false);
        var buffer = new byte[3];
        foreach (var expected in new byte[] { 65, 66 })
        {
            var read = useAsync
                ? await stream.ReadAsync(buffer, 1, 1, CancellationToken.None)
                : stream.Read(buffer, 1, 1);
            Assert.Equal(1, read);
            Assert.Equal(expected, buffer[1]);
            Assert.Equal(0, buffer[0]);
        }
        Assert.Equal(0, stream.Read(buffer, 0, 1));
        Assert.Equal(99, input.ReadByte());
    }

    [Fact]
    public void Arc_Rejects_Negative_Compressed_Size() =>
        Assert.Throws<InvalidFormatException>(() => new ArcLzwStream(Stream.Null, -1));
}
