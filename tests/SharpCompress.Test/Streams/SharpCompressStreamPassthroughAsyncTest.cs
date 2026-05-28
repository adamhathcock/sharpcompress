using System;
using System.IO;
using System.Threading.Tasks;
using SharpCompress.Common;
using SharpCompress.IO;
using SharpCompress.Test.Mocks;
using Xunit;

namespace SharpCompress.Test.Streams;

public class SharpCompressStreamPassthroughAsyncTest
{
    [Fact]
    public async ValueTask CreateNonDisposing_ReadAsync_DelegatesDirectly()
    {
        var ms = new MemoryStream(new byte[] { 1, 2, 3, 4, 5 });
        var stream = SharpCompressStream.CreateNonDisposing(ms);
        var buffer = new byte[5];
        int bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length).ConfigureAwait(false);
        Assert.Equal(5, bytesRead);
        Assert.Equal(new byte[] { 1, 2, 3, 4, 5 }, buffer);
    }

    [Fact]
    public async ValueTask CreateNonDisposing_WriteAsync_DelegatesDirectly()
    {
        var ms = new MemoryStream();
        var stream = SharpCompressStream.CreateNonDisposing(ms);
        var data = new byte[] { 1, 2, 3, 4, 5 };
        await stream.WriteAsync(data, 0, data.Length).ConfigureAwait(false);
        Assert.Equal(data, ms.ToArray());
    }

    [Fact]
    public async ValueTask CreateNonDisposing_FlushAsync_DelegatesDirectly()
    {
        var ms = new MemoryStream();
        var stream = SharpCompressStream.CreateNonDisposing(ms);
        await stream.WriteAsync(new byte[] { 1, 2, 3 }, 0, 3).ConfigureAwait(false);
        await stream.FlushAsync().ConfigureAwait(false);
        Assert.Equal(3, ms.Length);
    }

    [Fact]
    public async ValueTask CreateNonDisposing_CanReadAsync_ReturnsTrue()
    {
        var ms = new MemoryStream(new byte[] { 1, 2, 3, 4, 5 });
        var stream = SharpCompressStream.CreateNonDisposing(ms);
        Assert.True(stream.CanRead);
    }

    [Fact]
    public async ValueTask CreateNonDisposing_ReadAsync_WithCancellationToken()
    {
        var ms = new MemoryStream(new byte[] { 1, 2, 3, 4, 5 });
        var stream = SharpCompressStream.CreateNonDisposing(ms);
        var buffer = new byte[5];
        var cts = new System.Threading.CancellationTokenSource();
        int bytesRead = await stream
            .ReadAsync(buffer, 0, buffer.Length, cts.Token)
            .ConfigureAwait(false);
        Assert.Equal(5, bytesRead);
    }

    [Fact]
    public async ValueTask CreateNonDisposing_WriteAsync_WithCancellationToken()
    {
        var ms = new MemoryStream();
        var stream = SharpCompressStream.CreateNonDisposing(ms);
        var data = new byte[] { 1, 2, 3, 4, 5 };
        var cts = new System.Threading.CancellationTokenSource();
        await stream.WriteAsync(data, 0, data.Length, cts.Token).ConfigureAwait(false);
        Assert.Equal(data, ms.ToArray());
    }

    [Fact]
    public async ValueTask CreateNonDisposing_FlushAsync_WithCancellationToken()
    {
        var ms = new MemoryStream();
        var stream = SharpCompressStream.CreateNonDisposing(ms);
        await stream.WriteAsync(new byte[] { 1, 2, 3 }, 0, 3).ConfigureAwait(false);
        var cts = new System.Threading.CancellationTokenSource();
        await stream.FlushAsync(cts.Token).ConfigureAwait(false);
        Assert.Equal(3, ms.Length);
    }

#if !LEGACY_DOTNET
    [Fact]
    public async ValueTask CreateNonDisposing_DoesNotDisposeUnderlying_Async()
    {
        var ms = new MemoryStream(new byte[] { 1, 2, 3, 4, 5 });
        var stream = SharpCompressStream.CreateNonDisposing(ms);
        await stream.DisposeAsync().ConfigureAwait(false);
        Assert.Equal(0, ms.Position);
        Assert.True(ms.CanRead);
    }

    [Fact]
    public async ValueTask CreateNonDisposing_DisposeAsync_WithThrowOnDisposeTrue_ThrowsInvalidOperation()
    {
        var ms = new MemoryStream(new byte[] { 1, 2, 3, 4, 5 });
        var stream = SharpCompressStream.CreateNonDisposing(ms);
        stream.ThrowOnDispose = true;
        await Assert
            .ThrowsAsync<ArchiveOperationException>(async () =>
                await stream.DisposeAsync().ConfigureAwait(false)
            )
            .ConfigureAwait(false);
    }
#endif
}
