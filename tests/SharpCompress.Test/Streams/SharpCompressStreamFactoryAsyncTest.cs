using System;
using System.IO;
using System.Threading.Tasks;
using SharpCompress.IO;
using SharpCompress.Test.Mocks;
using Xunit;

namespace SharpCompress.Test.Streams;

public class SharpCompressStreamFactoryAsyncTest
{
    [Fact]
    public async ValueTask Create_AsyncReadWithSeekableStream_WorksCorrectly()
    {
        var ms = new MemoryStream(new byte[] { 1, 2, 3, 4, 5 });
        var stream = SharpCompressStream.Create(ms);
        var buffer = new byte[5];
        int bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length).ConfigureAwait(false);
        Assert.Equal(5, bytesRead);
        Assert.Equal(new byte[] { 1, 2, 3, 4, 5 }, buffer);
    }

    [Fact]
    public async ValueTask Create_AsyncReadWithNonSeekableStream_BufferedCorrectly()
    {
        var ms = new MemoryStream(new byte[] { 1, 2, 3, 4, 5 });
        var nonSeekableMs = new ForwardOnlyStream(ms);
        var stream = SharpCompressStream.Create(nonSeekableMs, 128);
        var buffer = new byte[5];
        int bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length).ConfigureAwait(false);
        Assert.Equal(5, bytesRead);
        Assert.Equal(new byte[] { 1, 2, 3, 4, 5 }, buffer);
    }

    [Fact]
    public async ValueTask Create_WithBufferAsync_WorksCorrectly()
    {
        var ms = new MemoryStream(new byte[] { 1, 2, 3, 4, 5 });
        var nonSeekableMs = new ForwardOnlyStream(ms);
        var stream = SharpCompressStream.Create(nonSeekableMs, 128);
        stream.StartRecording();
        var buffer = new byte[4];
        await stream.ReadAsync(buffer, 0, 4).ConfigureAwait(false);
        Assert.Equal(4, stream.Position);
    }

    [Fact]
    public async ValueTask Create_AsyncReadSeekable_PositionUpdatesCorrectly()
    {
        var ms = new MemoryStream(new byte[] { 1, 2, 3, 4, 5, 6, 7, 8 });
        var stream = SharpCompressStream.Create(ms);
        var buffer = new byte[4];
        await stream.ReadAsync(buffer, 0, 4).ConfigureAwait(false);
        Assert.Equal(4, stream.Position);
    }
}
