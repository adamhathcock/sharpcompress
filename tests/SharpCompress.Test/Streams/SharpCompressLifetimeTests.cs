using System;
using System.IO;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using SharpCompress.IO;
using Xunit;

namespace SharpCompress.Test.Streams;

public class SharpCompressLifetimeTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task FailedDisposeReleasesRingAndRejectsRecording(bool asyncDispose)
    {
        var source = new ThrowingDisposeStream();
        var stream = new SharpCompressStream(source);
        stream.StartRecording();
        var ring = typeof(SharpCompressStream).GetField(
            "_ringBuffer",
            BindingFlags.Instance | BindingFlags.NonPublic
        )!;
        Assert.NotNull(ring.GetValue(stream));
        if (asyncDispose)
        {
#if !LEGACY_DOTNET
            await Assert.ThrowsAsync<IOException>(async () => await stream.DisposeAsync());
#else
            Assert.Throws<IOException>(() => stream.Dispose());
            await Task.CompletedTask;
#endif
        }
        else
        {
            Assert.Throws<IOException>(() => stream.Dispose());
        }
        Assert.Null(ring.GetValue(stream));
        stream.Dispose();
        Assert.Throws<ObjectDisposedException>(() => stream.StartRecording());
        Assert.Throws<ObjectDisposedException>(() => stream.Read(new byte[1], 0, 1));
        await Assert.ThrowsAsync<ObjectDisposedException>(() =>
            stream.ReadAsync(new byte[1], 0, 1, CancellationToken.None)
        );
    }

    private sealed class ThrowingDisposeStream : MemoryStream
    {
        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);
            throw new IOException("Injected disposal failure");
        }
    }
}
