using System;
using System.IO;
using SharpCompress.Common;
using SharpCompress.IO;
using SharpCompress.Test.Mocks;
using Xunit;

namespace SharpCompress.Test.Streams;

public class SharpCompressStreamPassthroughTest
{
    [Fact]
    public void CreateNonDisposing_LeaveStreamOpen_ReturnsTrue()
    {
        var ms = new MemoryStream(new byte[] { 1, 2, 3, 4, 5 });
        var stream = SharpCompressStream.CreateNonDisposing(ms);
        Assert.True(stream.LeaveStreamOpen);
    }

    [Fact]
    public void CreateNonDisposing_IsPassthrough_ReturnsTrue()
    {
        var ms = new MemoryStream(new byte[] { 1, 2, 3, 4, 5 });
        var stream = SharpCompressStream.CreateNonDisposing(ms);
        Assert.True(stream.IsPassthrough);
    }

    [Fact]
    public void CreateNonDisposing_CanRead_ReturnsTrue()
    {
        var ms = new MemoryStream(new byte[] { 1, 2, 3, 4, 5 });
        var stream = SharpCompressStream.CreateNonDisposing(ms);
        Assert.True(stream.CanRead);
    }

    [Fact]
    public void CreateNonDisposing_CanSeek_DelegatesToUnderlyingSeekableStream()
    {
        var ms = new MemoryStream(new byte[] { 1, 2, 3, 4, 5 });
        var stream = SharpCompressStream.CreateNonDisposing(ms);
        Assert.Equal(ms.CanSeek, stream.CanSeek);
        Assert.True(stream.CanSeek);
    }

    [Fact]
    public void CreateNonDisposing_CanSeek_DelegatesToUnderlyingNonSeekableStream()
    {
        var ms = new MemoryStream(new byte[] { 1, 2, 3, 4, 5 });
        var nonSeekableMs = new ForwardOnlyStream(ms);
        var stream = SharpCompressStream.CreateNonDisposing(nonSeekableMs);
        Assert.Equal(nonSeekableMs.CanSeek, stream.CanSeek);
        Assert.False(stream.CanSeek);
    }

    [Fact]
    public void CreateNonDisposing_CanWrite_DelegatesToUnderlyingStream()
    {
        var ms = new MemoryStream();
        var stream = SharpCompressStream.CreateNonDisposing(ms);
        Assert.Equal(ms.CanWrite, stream.CanWrite);
        Assert.True(stream.CanWrite);
    }

    [Fact]
    public void CreateNonDisposing_Read_DelegatesDirectlyWithoutBuffering()
    {
        var ms = new MemoryStream(new byte[] { 1, 2, 3, 4, 5 });
        var stream = SharpCompressStream.CreateNonDisposing(ms);
        var buffer = new byte[5];
        int bytesRead = stream.Read(buffer, 0, buffer.Length);
        Assert.Equal(5, bytesRead);
        Assert.Equal(new byte[] { 1, 2, 3, 4, 5 }, buffer);
    }

    [Fact]
    public void CreateNonDisposing_PositionGet_DelegatesToUnderlyingStream()
    {
        var ms = new MemoryStream(new byte[] { 1, 2, 3, 4, 5 });
        ms.Position = 2;
        var stream = SharpCompressStream.CreateNonDisposing(ms);
        Assert.Equal(ms.Position, stream.Position);
        Assert.Equal(2, stream.Position);
    }

    [Fact]
    public void CreateNonDisposing_PositionSet_DelegatesToUnderlyingStream()
    {
        var ms = new MemoryStream(new byte[] { 1, 2, 3, 4, 5 });
        var stream = SharpCompressStream.CreateNonDisposing(ms);
        stream.Position = 3;
        Assert.Equal(3, ms.Position);
        Assert.Equal(3, stream.Position);
    }

    [Fact]
    public void CreateNonDisposing_DoesNotDisposeUnderlyingStream()
    {
        var ms = new MemoryStream(new byte[] { 1, 2, 3, 4, 5 });
        var stream = SharpCompressStream.CreateNonDisposing(ms);
        stream.Dispose();
        Assert.Equal(0, ms.Position);
        Assert.True(ms.CanRead);
    }

    [Fact]
    public void CreateNonDisposing_Length_DelegatesToUnderlyingStream()
    {
        var ms = new MemoryStream(new byte[] { 1, 2, 3, 4, 5 });
        var stream = SharpCompressStream.CreateNonDisposing(ms);
        Assert.Equal(ms.Length, stream.Length);
        Assert.Equal(5, stream.Length);
    }

    [Fact]
    public void CreateNonDisposing_Seek_DelegatesToUnderlyingStream()
    {
        var ms = new MemoryStream(new byte[] { 1, 2, 3, 4, 5 });
        var stream = SharpCompressStream.CreateNonDisposing(ms);
        long result = stream.Seek(3, SeekOrigin.Begin);
        Assert.Equal(3, result);
        Assert.Equal(3, ms.Position);
        Assert.Equal(3, stream.Position);
    }

    [Fact]
    public void CreateNonDisposing_Flush_DelegatesToUnderlyingStream()
    {
        var ms = new MemoryStream();
        var stream = SharpCompressStream.CreateNonDisposing(ms);
        stream.Write(new byte[] { 1, 2, 3 }, 0, 3);
        stream.Flush();
        Assert.Equal(3, ms.Length);
    }

    [Fact]
    public void CreateNonDisposing_SetLength_DelegatesToUnderlyingStream()
    {
        var ms = new MemoryStream();
        var stream = SharpCompressStream.CreateNonDisposing(ms);
        stream.SetLength(20);
        Assert.Equal(20, stream.Length);
        Assert.Equal(20, ms.Length);
    }

    [Fact]
    public void CreateNonDisposing_Write_DelegatesToUnderlyingStream()
    {
        var ms = new MemoryStream();
        var stream = SharpCompressStream.CreateNonDisposing(ms);
        var data = new byte[] { 1, 2, 3, 4, 5 };
        stream.Write(data, 0, data.Length);
        Assert.Equal(data, ms.ToArray());
    }

    [Fact]
    public void CreateNonDisposing_StartRecording_ThrowsInvalidOperation()
    {
        var ms = new MemoryStream(new byte[] { 1, 2, 3, 4, 5 });
        var stream = SharpCompressStream.CreateNonDisposing(ms);
        Assert.Throws<ArchiveOperationException>(() => stream.StartRecording());
    }

    [Fact]
    public void CreateNonDisposing_Rewind_ThrowsInvalidOperation()
    {
        var ms = new MemoryStream(new byte[] { 1, 2, 3, 4, 5 });
        var stream = SharpCompressStream.CreateNonDisposing(ms);
        Assert.Throws<ArchiveOperationException>(() => stream.Rewind());
    }

    [Fact]
    public void CreateNonDisposing_StopRecording_ThrowsInvalidOperation()
    {
        var ms = new MemoryStream(new byte[] { 1, 2, 3, 4, 5 });
        var stream = SharpCompressStream.CreateNonDisposing(ms);
        Assert.Throws<ArchiveOperationException>(() => stream.StopRecording());
    }

    [Fact]
    public void CreateNonDisposing_IsRecording_AlwaysFalse()
    {
        var ms = new MemoryStream(new byte[] { 1, 2, 3, 4, 5 });
        var stream = SharpCompressStream.CreateNonDisposing(ms);
        Assert.False(stream.IsRecording);
    }
}
