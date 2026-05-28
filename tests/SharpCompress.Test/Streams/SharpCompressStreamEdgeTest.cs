using System;
using System.IO;
using SharpCompress.Common;
using SharpCompress.IO;
using SharpCompress.Test.Mocks;
using Xunit;

namespace SharpCompress.Test.Streams;

public class SharpCompressStreamEdgeTest
{
    [Fact]
    public void Dispose_WithLeaveStreamOpenTrue_DoesNotDisposeUnderlying()
    {
        var ms = new MemoryStream(new byte[] { 1, 2, 3, 4, 5 });
        var stream = SharpCompressStream.CreateNonDisposing(ms);
        stream.Dispose();
        Assert.Equal(0, ms.Position);
        Assert.True(ms.CanRead);
    }

    [Fact]
    public void Dispose_WithLeaveStreamOpenFalse_DisposesUnderlying()
    {
        var ms = new MemoryStream(new byte[] { 1, 2, 3, 4, 5 });
        var nonSeekableMs = new ForwardOnlyStream(ms);
        var stream = SharpCompressStream.Create(nonSeekableMs, 128);
        stream.Dispose();
        Assert.Throws<ObjectDisposedException>(() => ms.Read(new byte[1], 0, 1));
    }

    [Fact]
    public void Dispose_WithThrowOnDisposeTrue_ThrowsInvalidOperation()
    {
        var ms = new MemoryStream(new byte[] { 1, 2, 3, 4, 5 });
        var stream = SharpCompressStream.CreateNonDisposing(ms);
        stream.ThrowOnDispose = true;
        Assert.Throws<ArchiveOperationException>(() => stream.Dispose());
    }

    [Fact]
    public void Read_ZeroCount_ReturnsZero()
    {
        var ms = new MemoryStream(new byte[] { 1, 2, 3, 4, 5 });
        var stream = SharpCompressStream.CreateNonDisposing(ms);
        var buffer = new byte[10];
        int bytesRead = stream.Read(buffer, 0, 0);
        Assert.Equal(0, bytesRead);
    }

    [Fact]
    public void Read_AtEndOfStream_ReturnsZero()
    {
        var ms = new MemoryStream(new byte[] { 1, 2, 3, 4, 5 });
        var stream = SharpCompressStream.CreateNonDisposing(ms);
        var buffer = new byte[10];
        int bytesRead = stream.Read(buffer, 0, buffer.Length);
        Assert.Equal(5, bytesRead);
        bytesRead = stream.Read(buffer, 0, buffer.Length);
        Assert.Equal(0, bytesRead);
    }

    [Fact]
    public void Position_InitialValue_IsZero()
    {
        var ms = new MemoryStream(new byte[] { 1, 2, 3, 4, 5 });
        var nonSeekableMs = new ForwardOnlyStream(ms);
        var stream = SharpCompressStream.Create(nonSeekableMs, 128);
        Assert.Equal(0, stream.Position);
    }

    [Fact]
    public void CanRead_AlwaysReturnsTrue()
    {
        var ms = new MemoryStream(new byte[] { 1, 2, 3, 4, 5 });
        var stream = SharpCompressStream.CreateNonDisposing(ms);
        Assert.True(stream.CanRead);
    }

    [Fact]
    public void CanSeek_PassthroughMode_DelegatesToUnderlying()
    {
        var seekableMs = new MemoryStream(new byte[] { 1, 2, 3, 4, 5 });
        var nonSeekableMs = new ForwardOnlyStream(new MemoryStream(new byte[] { 1, 2, 3, 4, 5 }));

        var seekableStream = SharpCompressStream.CreateNonDisposing(seekableMs);
        var nonSeekableStream = SharpCompressStream.CreateNonDisposing(nonSeekableMs);

        Assert.True(seekableStream.CanSeek);
        Assert.False(nonSeekableStream.CanSeek);
    }

    [Fact]
    public void BaseStream_ReturnsUnderlyingStream()
    {
        var ms = new MemoryStream(new byte[] { 1, 2, 3, 4, 5 });
        var stream = SharpCompressStream.CreateNonDisposing(ms);
        Assert.Same(ms, stream.BaseStream());
    }
}
