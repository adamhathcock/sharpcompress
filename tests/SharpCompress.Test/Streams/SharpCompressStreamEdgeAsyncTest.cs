using System;
using System.IO;
using System.Threading.Tasks;
using SharpCompress.IO;
using SharpCompress.Test.Mocks;
using Xunit;

namespace SharpCompress.Test.Streams;

public class SharpCompressStreamEdgeAsyncTest
{
    [Fact]
    public async ValueTask DisposeAsync_WithLeaveStreamOpenTrue_DoesNotDisposeUnderlying()
    {
        var ms = new MemoryStream(new byte[] { 1, 2, 3, 4, 5 });
        var stream = SharpCompressStream.CreateNonDisposing(ms);
        await stream.DisposeAsync().ConfigureAwait(false);
        Assert.Equal(0, ms.Position);
        Assert.True(ms.CanRead);
    }

    [Fact]
    public async ValueTask DisposeAsync_WithLeaveStreamOpenFalse_DisposesUnderlying()
    {
        var ms = new MemoryStream(new byte[] { 1, 2, 3, 4, 5 });
        var nonSeekableMs = new ForwardOnlyStream(ms);
        var stream = SharpCompressStream.Create(nonSeekableMs, 128);
        await stream.DisposeAsync().ConfigureAwait(false);
        Assert.Throws<ObjectDisposedException>(() => ms.Read(new byte[1], 0, 1));
    }

    [Fact]
    public async ValueTask ReadAsync_ZeroCount_ReturnsZero()
    {
        var ms = new MemoryStream(new byte[] { 1, 2, 3, 4, 5 });
        var stream = SharpCompressStream.CreateNonDisposing(ms);
        var buffer = new byte[10];
        int bytesRead = await stream.ReadAsync(buffer, 0, 0).ConfigureAwait(false);
        Assert.Equal(0, bytesRead);
    }

    [Fact]
    public async ValueTask ReadAsync_AtEndOfStream_ReturnsZero()
    {
        var ms = new MemoryStream(new byte[] { 1, 2, 3, 4, 5 });
        var stream = SharpCompressStream.CreateNonDisposing(ms);
        var buffer = new byte[10];
        await stream.ReadAsync(buffer, 0, buffer.Length).ConfigureAwait(false);
        int bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length).ConfigureAwait(false);
        Assert.Equal(0, bytesRead);
    }

    [Fact]
    public async ValueTask ReadAsyncMemory_ZeroCount_ReturnsZero()
    {
        var ms = new MemoryStream(new byte[] { 1, 2, 3, 4, 5 });
        var stream = SharpCompressStream.CreateNonDisposing(ms);
        var buffer = new byte[10];
        int bytesRead = await stream.ReadAsync(buffer.AsMemory(0, 0)).ConfigureAwait(false);
        Assert.Equal(0, bytesRead);
    }

    [Fact]
    public async ValueTask ReadAsyncMemory_AtEndOfStream_ReturnsZero()
    {
        var ms = new MemoryStream(new byte[] { 1, 2, 3, 4, 5 });
        var stream = SharpCompressStream.CreateNonDisposing(ms);
        var buffer = new byte[10];
        await stream.ReadAsync(buffer.AsMemory()).ConfigureAwait(false);
        int bytesRead = await stream.ReadAsync(buffer.AsMemory()).ConfigureAwait(false);
        Assert.Equal(0, bytesRead);
    }

    [Fact]
    public async ValueTask CopyToAsync_DelegatesToUnderlyingStream()
    {
        var ms = new MemoryStream(new byte[] { 1, 2, 3, 4, 5 });
        var stream = SharpCompressStream.CreateNonDisposing(ms);
        var destination = new MemoryStream();
        await stream.CopyToAsync(destination).ConfigureAwait(false);
        Assert.Equal(new byte[] { 1, 2, 3, 4, 5 }, destination.ToArray());
    }

    [Fact]
    public async ValueTask CopyToAsync_WithBufferSize_WorksCorrectly()
    {
        var ms = new MemoryStream(new byte[] { 1, 2, 3, 4, 5 });
        var stream = SharpCompressStream.CreateNonDisposing(ms);
        var destination = new MemoryStream();
        await stream.CopyToAsync(destination, 2).ConfigureAwait(false);
        Assert.Equal(new byte[] { 1, 2, 3, 4, 5 }, destination.ToArray());
    }

    [Fact]
    public async ValueTask WriteAsyncMemory_DelegatesToUnderlying()
    {
        var ms = new MemoryStream();
        var stream = SharpCompressStream.CreateNonDisposing(ms);
        var data = new byte[] { 1, 2, 3, 4, 5 };
        await stream.WriteAsync(data.AsMemory()).ConfigureAwait(false);
        Assert.Equal(data, ms.ToArray());
    }

    [Fact]
    public async ValueTask FlushAsyncMemory_DelegatesToUnderlying()
    {
        var ms = new MemoryStream();
        var stream = SharpCompressStream.CreateNonDisposing(ms);
        await stream.WriteAsync(new byte[] { 1, 2, 3 }).ConfigureAwait(false);
        await stream.FlushAsync().ConfigureAwait(false);
        Assert.Equal(3, ms.Length);
    }
}
