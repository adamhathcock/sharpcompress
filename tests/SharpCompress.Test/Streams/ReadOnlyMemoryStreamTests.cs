using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using SharpCompress.IO;
using Xunit;

namespace SharpCompress.Test.Streams;

public class ReadOnlyMemoryStreamTests
{
    [Fact]
    public async Task Reads_Borrowed_Slice_Without_Copying()
    {
        var data = new byte[] { 99, 1, 2, 3, 99 };
        using var stream = new ReadOnlyMemoryStream(data.AsMemory(1, 3));
        data[1] = 4;
        Assert.Equal(4, stream.ReadByte());
        var buffer = new byte[4];
        Assert.Equal(2, await stream.ReadAsync(buffer, 1, 3, CancellationToken.None));
        Assert.Equal(new byte[] { 0, 2, 3, 0 }, buffer);
        Assert.Equal(3, stream.Position);
        Assert.Equal(0, stream.Read(buffer, 0, buffer.Length));
        Assert.Equal(-1, stream.ReadByte());
    }

    [Fact]
    public async Task Cancellation_And_Disposal_Do_Not_Consume_Memory()
    {
        var data = new byte[] { 1, 2, 3 };
        var stream = new ReadOnlyMemoryStream(data);
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            stream.ReadAsync(new byte[3], 0, 3, cts.Token)
        );
        Assert.Equal(0, stream.Position);
        stream.Dispose();
        Assert.False(stream.CanRead);
        Assert.Throws<ObjectDisposedException>(() => stream.ReadByte());
        Assert.Equal(new byte[] { 1, 2, 3 }, data);
    }
}
