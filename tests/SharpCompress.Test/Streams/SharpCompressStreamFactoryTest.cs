using System;
using System.IO;
using SharpCompress.IO;
using SharpCompress.Test.Mocks;
using Xunit;

namespace SharpCompress.Test.Streams;

public class SharpCompressStreamFactoryTest
{
    private class IStreamStackMock : Stream, IStreamStack
    {
        private readonly Stream _baseStream;

        public IStreamStackMock(Stream baseStream)
        {
            _baseStream = baseStream;
        }

        public Stream BaseStream() => _baseStream;

        public override bool CanRead => _baseStream.CanRead;

        public override bool CanSeek => _baseStream.CanSeek;

        public override bool CanWrite => _baseStream.CanWrite;

        public override long Length => _baseStream.Length;

        public override long Position
        {
            get => _baseStream.Position;
            set => _baseStream.Position = value;
        }

        public override void Flush() => _baseStream.Flush();

        public override int Read(byte[] buffer, int offset, int count) =>
            _baseStream.Read(buffer, offset, count);

        public override long Seek(long offset, SeekOrigin origin) =>
            _baseStream.Seek(offset, origin);

        public override void SetLength(long value) => _baseStream.SetLength(value);

        public override void Write(byte[] buffer, int offset, int count) =>
            _baseStream.Write(buffer, offset, count);
    }

    [Fact]
    public void Create_WithSeekableStream_ReturnsSeekableSharpCompressStream()
    {
        var ms = new MemoryStream(new byte[] { 1, 2, 3, 4, 5 });
        var stream = SharpCompressStream.Create(ms);
        Assert.IsType<SeekableSharpCompressStream>(stream);
    }

    [Fact]
    public void Create_WithNonSeekableStream_ReturnsSharpCompressStreamWithBuffer()
    {
        var ms = new MemoryStream(new byte[] { 1, 2, 3, 4, 5 });
        var nonSeekableMs = new ForwardOnlyStream(ms);
        var stream = SharpCompressStream.Create(nonSeekableMs);
        Assert.IsType<SharpCompressStream>(stream);
        Assert.NotNull(stream);
    }

    [Fact]
    public void Create_WithSharpCompressStreamPassthrough_UnwrapsAndCreatesNew()
    {
        var ms = new MemoryStream(new byte[] { 1, 2, 3, 4, 5 });
        var passthroughStream = SharpCompressStream.CreateNonDisposing(ms);
        var stream = SharpCompressStream.Create(passthroughStream);
        Assert.NotSame(passthroughStream, stream);
        Assert.IsType<SeekableSharpCompressStream>(stream);
    }

    [Fact]
    public void Create_WithSharpCompressStreamNonPassthrough_ReturnsSameInstance()
    {
        var ms = new MemoryStream(new byte[] { 1, 2, 3, 4, 5 });
        var nonSeekableMs = new ForwardOnlyStream(ms);
        var sharpStream = SharpCompressStream.Create(nonSeekableMs, 128);
        var stream = SharpCompressStream.Create(sharpStream);
        Assert.Same(sharpStream, stream);
    }

    [Fact]
    public void Create_WithIStreamStack_UnwrapsSharpCompressStream()
    {
        var ms = new MemoryStream(new byte[] { 1, 2, 3, 4, 5 });
        var sharpStream = SharpCompressStream.CreateNonDisposing(ms);
        var wrappedStream = new IStreamStackMock(sharpStream);
        var stream = SharpCompressStream.Create(wrappedStream);
        Assert.Same(sharpStream, stream);
    }

    [Fact]
    public void Create_WithBufferSize_UsesCustomBufferSize()
    {
        var ms = new MemoryStream(new byte[] { 1, 2, 3, 4, 5 });
        var nonSeekableMs = new ForwardOnlyStream(ms);
        var stream = SharpCompressStream.Create(nonSeekableMs, 128);
        Assert.NotNull(stream);
        stream.StartRecording();
        var buffer = new byte[4];
        stream.Read(buffer, 0, 4);
        Assert.Equal(4, stream.Position);
    }

    [Fact]
    public void Create_WithLeaveStreamOpenTrue_PreservesSetting()
    {
        var ms = new MemoryStream(new byte[] { 1, 2, 3, 4, 5 });
        var passthroughStream = SharpCompressStream.CreateNonDisposing(ms);
        var stream = SharpCompressStream.Create(passthroughStream);
        Assert.True(stream.LeaveStreamOpen);
    }

    [Fact]
    public void Create_WithSeekablePassthroughStream_CreatesSeekableWrapper()
    {
        var ms = new MemoryStream(new byte[] { 1, 2, 3, 4, 5 });
        var passthroughStream = SharpCompressStream.CreateNonDisposing(ms);
        var stream = SharpCompressStream.Create(passthroughStream);
        Assert.IsType<SeekableSharpCompressStream>(stream);
    }

    [Fact]
    public void Create_WithIStreamStack_ReturnsUnderlyingSharpCompressStream()
    {
        var ms = new MemoryStream(new byte[] { 1, 2, 3, 4, 5 });
        var sharpStream = SharpCompressStream.Create(ms);
        var wrappedStream = new IStreamStackMock(sharpStream);
        var result = SharpCompressStream.Create(wrappedStream);
        Assert.Same(sharpStream, result);
    }

    [Fact]
    public void Create_WithNonSeekablePassthroughStream_CreatesBufferedWrapper()
    {
        var ms = new MemoryStream(new byte[] { 1, 2, 3, 4, 5 });
        var nonSeekableMs = new ForwardOnlyStream(ms);
        var passthroughStream = SharpCompressStream.CreateNonDisposing(nonSeekableMs);
        var stream = SharpCompressStream.Create(passthroughStream);
        Assert.IsType<SharpCompressStream>(stream);
    }
}
