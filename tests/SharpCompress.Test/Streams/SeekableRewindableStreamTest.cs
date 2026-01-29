using System;
using System.IO;
using System.Linq;
using SharpCompress.IO;
using SharpCompress.Test.Mocks;
using Xunit;

namespace SharpCompress.Test.Streams;

public class SeekableRewindableStreamTest
{
    [Fact]
    public void Constructor_ThrowsOnNullStream()
    {
        Assert.Throws<ArgumentNullException>(() => new SeekableRewindableStream(null!));
    }

    [Fact]
    public void Constructor_ThrowsOnNonSeekableStream()
    {
        var nonSeekable = new ForwardOnlyStream(new MemoryStream());
        Assert.Throws<ArgumentException>(() => new SeekableRewindableStream(nonSeekable));
    }

    [Fact]
    public void Constructor_AcceptsSeekableStream()
    {
        var ms = new MemoryStream();
        var stream = new SeekableRewindableStream(ms);
        Assert.NotNull(stream);
    }

    [Fact]
    public void CanRead_DelegatesToUnderlyingStream()
    {
        var ms = new MemoryStream();
        var stream = new SeekableRewindableStream(ms);
        Assert.Equal(ms.CanRead, stream.CanRead);
    }

    [Fact]
    public void CanSeek_DelegatesToUnderlyingStream()
    {
        var ms = new MemoryStream();
        var stream = new SeekableRewindableStream(ms);
        Assert.Equal(ms.CanSeek, stream.CanSeek);
    }

    [Fact]
    public void CanWrite_DelegatesToUnderlyingStream()
    {
        var ms = new MemoryStream();
        var stream = new SeekableRewindableStream(ms);
        Assert.Equal(ms.CanWrite, stream.CanWrite);
    }

    [Fact]
    public void Length_DelegatesToUnderlyingStream()
    {
        var ms = new MemoryStream(new byte[] { 1, 2, 3, 4, 5 });
        var stream = new SeekableRewindableStream(ms);
        Assert.Equal(5, stream.Length);
    }

    [Fact]
    public void Position_Getter_DelegatesToUnderlyingStream()
    {
        var ms = new MemoryStream(new byte[] { 1, 2, 3, 4, 5 });
        ms.Position = 2;
        var stream = new SeekableRewindableStream(ms);
        Assert.Equal(2, stream.Position);
    }

    [Fact]
    public void Position_Setter_DelegatesToUnderlyingStream()
    {
        var ms = new MemoryStream(new byte[] { 1, 2, 3, 4, 5 });
        var stream = new SeekableRewindableStream(ms);
        stream.Position = 3;
        Assert.Equal(3, ms.Position);
        Assert.Equal(3, stream.Position);
    }

    [Fact]
    public void IsRecording_AlwaysFalse()
    {
        var ms = new MemoryStream();
        var stream = new SeekableRewindableStream(ms);
        Assert.False(stream.IsRecording);
    }

    [Fact]
    public void Read_Buffers()
    {
        var ms = new MemoryStream(new byte[] { 1, 2, 3, 4, 5 });
        var stream = new SeekableRewindableStream(ms);
        var buffer = new byte[5];
        int bytesRead = stream.Read(buffer, 0, buffer.Length);
        Assert.Equal(5, bytesRead);
        Assert.Equal(new byte[] { 1, 2, 3, 4, 5 }, buffer);
    }

    [Fact]
    public void Seek_DelegatesToUnderlyingStream()
    {
        var ms = new MemoryStream(new byte[] { 1, 2, 3, 4, 5 });
        var stream = new SeekableRewindableStream(ms);
        long result = stream.Seek(3, SeekOrigin.Begin);
        Assert.Equal(3, result);
        Assert.Equal(3, ms.Position);
    }

    [Fact]
    public void SetLength_DelegatesToUnderlyingStream()
    {
        var ms = new MemoryStream();
        var stream = new SeekableRewindableStream(ms);
        stream.SetLength(20);
        Assert.Equal(20, stream.Length);
        Assert.Equal(20, ms.Length);
    }

    [Fact]
    public void Write_DelegatesToUnderlyingStream()
    {
        var ms = new MemoryStream();
        var stream = new SeekableRewindableStream(ms);
        var data = new byte[] { 1, 2, 3, 4, 5 };
        stream.Write(data, 0, data.Length);
        Assert.Equal(data, ms.ToArray());
    }

    [Fact]
    public void Rewind_IsNoOp()
    {
        var ms = new MemoryStream(new byte[] { 1, 2, 3, 4, 5 });
        var stream = new SeekableRewindableStream(ms);
        stream.Rewind();
        Assert.Equal(0, stream.Position);
        ms.Position = 2;
        stream.Rewind(true);
        Assert.Equal(2, stream.Position);
    }

    [Fact]
    public void StartRecording_IsNoOp()
    {
        var ms = new MemoryStream(new byte[] { 1, 2, 3, 4, 5 });
        var stream = new SeekableRewindableStream(ms);
        stream.StartRecording();
        Assert.False(stream.IsRecording);
    }

    [Fact]
    public void StopRecording_IsNoOp()
    {
        var ms = new MemoryStream(new byte[] { 1, 2, 3, 4, 5 });
        var stream = new SeekableRewindableStream(ms);
        stream.StopRecording();
        Assert.False(stream.IsRecording);
    }

    [Fact]
    public void Dispose_DisposesUnderlyingStream()
    {
        var ms = new MemoryStream(new byte[] { 1, 2, 3, 4, 5 });
        var stream = new SeekableRewindableStream(ms);
        stream.Dispose();
        Assert.Throws<ObjectDisposedException>(() => ms.Read(new byte[1], 0, 1));
    }

    [Fact]
    public void ReadAndSeek_MultipleOperations()
    {
        var ms = new MemoryStream(new byte[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10 });
        var stream = new SeekableRewindableStream(ms);

        var buffer = new byte[3];
        stream.Read(buffer, 0, 3);
        Assert.Equal(new byte[] { 1, 2, 3 }, buffer);
        Assert.Equal(3, stream.Position);

        stream.Seek(7, SeekOrigin.Begin);
        Assert.Equal(7, stream.Position);

        Array.Clear(buffer, 0, buffer.Length);
        stream.Read(buffer, 0, 2);
        Assert.Equal(new byte[] { 8, 9, 0 }, buffer);
        Assert.Equal(9, stream.Position);
    }

    [Fact]
    public void SeekWithDifferentOrigins()
    {
        var ms = new MemoryStream(new byte[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10 });
        var stream = new SeekableRewindableStream(ms);

        stream.Seek(3, SeekOrigin.Begin);
        Assert.Equal(3, stream.Position);

        stream.Seek(2, SeekOrigin.Current);
        Assert.Equal(5, stream.Position);

        stream.Seek(-3, SeekOrigin.End);
        Assert.Equal(7, stream.Position);
    }

    [Fact]
    public void WriteAndRead_WrittenDataIsReadable()
    {
        var ms = new MemoryStream();
        var stream = new SeekableRewindableStream(ms);

        var writeData = new byte[] { 1, 2, 3, 4, 5 };
        stream.Write(writeData, 0, writeData.Length);

        stream.Position = 0;
        var readBuffer = new byte[5];
        stream.Read(readBuffer, 0, 5);
        Assert.Equal(writeData, readBuffer);
    }

    [Fact]
    public void RecordingOperationsDoNotAffectStream()
    {
        var ms = new MemoryStream(new byte[] { 1, 2, 3, 4, 5 });
        var stream = new SeekableRewindableStream(ms);

        stream.StartRecording();
        var buffer = new byte[3];
        stream.Read(buffer, 0, 3);
        Assert.Equal(new byte[] { 1, 2, 3 }, buffer);
        Assert.Equal(3, stream.Position);

        stream.Rewind(true);
        Assert.Equal(3, stream.Position);

        var buffer2 = new byte[2];
        stream.Read(buffer2, 0, 2);
        Assert.Equal(new byte[] { 4, 5 }, buffer2);
        Assert.Equal(5, stream.Position);
    }
}

#if !LEGACY_DOTNET
public partial class SeekableRewindableSpanTest
{
    [Fact]
    public void Read_Span()
    {
        var ms = new MemoryStream(new byte[] { 1, 2, 3, 4, 5 });
        var stream = new SeekableRewindableStream(ms);
        var buffer = new byte[5];
        int bytesRead = stream.Read(buffer);
        Assert.Equal(5, bytesRead);
        Assert.Equal(new byte[] { 1, 2, 3, 4, 5 }, buffer);
    }

    [Fact]
    public void Write_Span()
    {
        var ms = new MemoryStream();
        var stream = new SeekableRewindableStream(ms);
        var data = new byte[] { 1, 2, 3, 4, 5 };
        stream.Write(data);
        Assert.Equal(data, ms.ToArray());
    }

    [Fact]
    public void ReadAndWrite_SpanOperations()
    {
        var ms = new MemoryStream();
        var stream = new SeekableRewindableStream(ms);

        var writeData = new byte[] { 1, 2, 3, 4, 5 };
        stream.Write(writeData);

        stream.Position = 0;
        var readBuffer = new byte[5];
        stream.Read(readBuffer);
        Assert.Equal(writeData, readBuffer);
    }
}
#endif
