using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using SharpCompress.IO;
using Xunit;

namespace SharpCompress.Test.Streams;

public class SeekableRewindableStreamAsyncTest
{
    [Fact]
    public async Task ReadAsync_Buffers()
    {
        var ms = new MemoryStream(new byte[] { 1, 2, 3, 4, 5 });
        var stream = new SeekableRewindableStream(ms);
        var buffer = new byte[5];
        int bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length).ConfigureAwait(false);
        Assert.Equal(5, bytesRead);
        Assert.Equal(new byte[] { 1, 2, 3, 4, 5 }, buffer);
    }

    [Fact]
    public async Task ReadAsync_WithCancellation()
    {
        var ms = new MemoryStream(new byte[] { 1, 2, 3, 4, 5 });
        var stream = new SeekableRewindableStream(ms);
        var buffer = new byte[5];
        var cts = new CancellationTokenSource();
        int bytesRead = await stream
            .ReadAsync(buffer, 0, buffer.Length, cts.Token)
            .ConfigureAwait(false);
        Assert.Equal(5, bytesRead);
        Assert.Equal(new byte[] { 1, 2, 3, 4, 5 }, buffer);
    }

    [Fact]
    public async Task ReadAsync_PartialRead()
    {
        var ms = new MemoryStream(new byte[] { 1, 2, 3, 4, 5 });
        var stream = new SeekableRewindableStream(ms);
        var buffer = new byte[10];
        int bytesRead = await stream.ReadAsync(buffer, 0, 10).ConfigureAwait(false);
        Assert.Equal(5, bytesRead);
        Assert.Equal(new byte[] { 1, 2, 3, 4, 5 }, buffer.Take(5).ToArray());
    }

    [Fact]
    public async Task WriteAsync_Buffers()
    {
        var ms = new MemoryStream();
        var stream = new SeekableRewindableStream(ms);
        var data = new byte[] { 1, 2, 3, 4, 5 };
        await stream.WriteAsync(data, 0, data.Length).ConfigureAwait(false);
        Assert.Equal(data, ms.ToArray());
    }

    [Fact]
    public async Task WriteAsync_WithCancellation()
    {
        var ms = new MemoryStream();
        var stream = new SeekableRewindableStream(ms);
        var data = new byte[] { 1, 2, 3, 4, 5 };
        var cts = new CancellationTokenSource();
        await stream.WriteAsync(data, 0, data.Length, cts.Token).ConfigureAwait(false);
        Assert.Equal(data, ms.ToArray());
    }

    [Fact]
    public async Task FlushAsync_DelegatesToUnderlyingStream()
    {
        var ms = new MemoryStream();
        var stream = new SeekableRewindableStream(ms);
        var data = new byte[] { 1, 2, 3, 4, 5 };
        await stream.WriteAsync(data, 0, data.Length).ConfigureAwait(false);
        await stream.FlushAsync().ConfigureAwait(false);
        Assert.Equal(5, ms.Length);
    }

    [Fact]
    public async Task CopyToAsync_CopiesAllData()
    {
        var sourceMs = new MemoryStream(new byte[] { 1, 2, 3, 4, 5 });
        var stream = new SeekableRewindableStream(sourceMs);
        var destinationMs = new MemoryStream();
        await stream.CopyToAsync(destinationMs, 4096).ConfigureAwait(false);
        Assert.Equal(new byte[] { 1, 2, 3, 4, 5 }, destinationMs.ToArray());
    }

    [Fact]
    public async Task ReadAsyncAndSeek_MultipleOperations()
    {
        var ms = new MemoryStream(new byte[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10 });
        var stream = new SeekableRewindableStream(ms);

        var buffer = new byte[3];
        await stream.ReadAsync(buffer, 0, 3).ConfigureAwait(false);
        Assert.Equal(new byte[] { 1, 2, 3 }, buffer);
        Assert.Equal(3, stream.Position);

        stream.Seek(7, SeekOrigin.Begin);
        Assert.Equal(7, stream.Position);

        Array.Clear(buffer, 0, buffer.Length);
        await stream.ReadAsync(buffer, 0, 2).ConfigureAwait(false);
        Assert.Equal(new byte[] { 8, 9, 0 }, buffer);
        Assert.Equal(9, stream.Position);
    }

    [Fact]
    public async Task WriteAsyncAndReadAsync_WrittenDataIsReadable()
    {
        var ms = new MemoryStream();
        var stream = new SeekableRewindableStream(ms);

        var writeData = new byte[] { 1, 2, 3, 4, 5 };
        await stream.WriteAsync(writeData, 0, writeData.Length).ConfigureAwait(false);

        stream.Position = 0;
        var readBuffer = new byte[5];
        await stream.ReadAsync(readBuffer, 0, 5).ConfigureAwait(false);
        Assert.Equal(writeData, readBuffer);
    }

    [Fact]
    public async Task AsyncOperationsDoNotCauseRecording()
    {
        var ms = new MemoryStream(new byte[] { 1, 2, 3, 4, 5 });
        var stream = new SeekableRewindableStream(ms);

        stream.StartRecording();
        var buffer = new byte[3];
        await stream.ReadAsync(buffer, 0, 3).ConfigureAwait(false);
        Assert.Equal(new byte[] { 1, 2, 3 }, buffer);
        Assert.Equal(3, stream.Position);

        stream.Rewind(true);
        Assert.Equal(3, stream.Position);

        var buffer2 = new byte[2];
        await stream.ReadAsync(buffer2, 0, 2).ConfigureAwait(false);
        Assert.Equal(new byte[] { 4, 5 }, buffer2);
        Assert.Equal(5, stream.Position);
    }
}

#if !LEGACY_DOTNET
public partial class SeekableRewindableStreamMemoryAsyncTest
{
    [Fact]
    public async ValueTask ReadAsync_Memory()
    {
        var ms = new MemoryStream(new byte[] { 1, 2, 3, 4, 5 });
        var stream = new SeekableRewindableStream(ms);
        var buffer = new byte[5];
        int bytesRead = await stream.ReadAsync(buffer).ConfigureAwait(false);
        Assert.Equal(5, bytesRead);
        Assert.Equal(new byte[] { 1, 2, 3, 4, 5 }, buffer);
    }

    [Fact]
    public async ValueTask ReadAsync_Memory_WithCancellation()
    {
        var ms = new MemoryStream(new byte[] { 1, 2, 3, 4, 5 });
        var stream = new SeekableRewindableStream(ms);
        var buffer = new byte[5];
        var cts = new CancellationTokenSource();
        int bytesRead = await stream.ReadAsync(buffer, cts.Token).ConfigureAwait(false);
        Assert.Equal(5, bytesRead);
        Assert.Equal(new byte[] { 1, 2, 3, 4, 5 }, buffer);
    }

    [Fact]
    public async ValueTask WriteAsync_Memory()
    {
        var ms = new MemoryStream();
        var stream = new SeekableRewindableStream(ms);
        var data = new byte[] { 1, 2, 3, 4, 5 };
        await stream.WriteAsync(data).ConfigureAwait(false);
        Assert.Equal(data, ms.ToArray());
    }

    [Fact]
    public async ValueTask WriteAsync_Memory_WithCancellation()
    {
        var ms = new MemoryStream();
        var stream = new SeekableRewindableStream(ms);
        var data = new byte[] { 1, 2, 3, 4, 5 };
        var cts = new CancellationTokenSource();
        await stream.WriteAsync(data, cts.Token).ConfigureAwait(false);
        Assert.Equal(data, ms.ToArray());
    }

    [Fact]
    public async ValueTask ReadMemoryAndWriteMemory_MemoryOperations()
    {
        var ms = new MemoryStream();
        var stream = new SeekableRewindableStream(ms);

        var writeData = new byte[] { 1, 2, 3, 4, 5 };
        await stream.WriteAsync(writeData).ConfigureAwait(false);

        stream.Position = 0;
        var readBuffer = new byte[5];
        await stream.ReadAsync(readBuffer).ConfigureAwait(false);
        Assert.Equal(writeData, readBuffer);
    }

    [Fact]
    public async ValueTask DisposeAsync_DisposesUnderlyingStream()
    {
        var ms = new MemoryStream(new byte[] { 1, 2, 3, 4, 5 });
        var stream = new SeekableRewindableStream(ms);
        await stream.DisposeAsync().ConfigureAwait(false);
        Assert.Throws<ObjectDisposedException>(() => ms.Read(new byte[1], 0, 1));
    }
}
#endif
