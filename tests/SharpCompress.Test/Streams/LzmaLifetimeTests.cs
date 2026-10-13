using System;
using System.IO;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using SharpCompress.Compressors.LZMA;
using Xunit;

namespace SharpCompress.Test.Streams;

public class LzmaLifetimeTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task FailedFinalizationReleasesEncoderBuffers(bool asyncDispose)
    {
        using var output = new ThrowingOutput();
        var stream = LzmaStream.Create(new LzmaEncoderProperties(), false, output);
        var encoder = GetField(stream, "_encoder")!;
        if (asyncDispose)
        {
            await Assert.ThrowsAsync<IOException>(async () => await stream.DisposeAsync());
        }
        else
        {
            Assert.Throws<IOException>(() => stream.Dispose());
        }
        Assert.Null(GetField(encoder, "_matchFinder"));
        stream.Dispose();
        Assert.False(stream.CanWrite);
        Assert.Throws<ObjectDisposedException>(() => stream.Write(new byte[1], 0, 1));
        await Assert.ThrowsAsync<ObjectDisposedException>(() =>
            stream.WriteAsync(new byte[1], 0, 1, CancellationToken.None)
        );
    }

    [Fact]
    public async Task DisposedLzma2ReaderCannotRentHeaderBuffer()
    {
        using var input = new MemoryStream(new byte[] { 0 });
        var stream = LzmaStream.Create(new byte[] { 0 }, input, leaveOpen: true);
        stream.Dispose();
        await Assert.ThrowsAsync<ObjectDisposedException>(() =>
            stream.ReadAsync(new byte[1], 0, 1, CancellationToken.None)
        );
        Assert.Null(GetField(stream, "_asyncHeaderBuffer"));
        Assert.False(stream.CanRead);
        Assert.True(input.CanRead);
    }

    [Fact]
    public void InvalidPropertiesLeaveInputOpen()
    {
        using var input = new MemoryStream();
        Assert.Throws<InvalidParamException>(() =>
            LzmaStream.Create(new byte[] { 225, 0, 0, 16, 0 }, input)
        );
        Assert.True(input.CanRead);
    }

    private static object? GetField(object instance, string name) =>
        instance
            .GetType()
            .GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!
            .GetValue(instance);

    private sealed class ThrowingOutput : MemoryStream
    {
        public override void WriteByte(byte value) =>
            throw new IOException("Injected output failure");

        public override Task WriteAsync(
            byte[] buffer,
            int offset,
            int count,
            CancellationToken cancellationToken
        ) => Task.FromException(new IOException("Injected output failure"));
    }
}
