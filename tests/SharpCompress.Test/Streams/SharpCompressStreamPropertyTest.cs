using System;
using System.IO;
using SharpCompress.IO;
using SharpCompress.Test.Mocks;
using Xunit;

namespace SharpCompress.Test.Streams;

public class SharpCompressStreamPropertyTest
{
    [Fact]
    public void BaseStream_ReturnsUnderlyingStream()
    {
        var ms = new MemoryStream(new byte[] { 1, 2, 3, 4, 5 });
        var stream = SharpCompressStream.CreateNonDisposing(ms);
        Assert.Same(ms, stream.BaseStream());
    }

    [Fact]
    public void IsPassthrough_CreateNonDisposing_ReturnsTrue()
    {
        var ms = new MemoryStream(new byte[] { 1, 2, 3, 4, 5 });
        var stream = SharpCompressStream.CreateNonDisposing(ms);
        Assert.True(stream.IsPassthrough);
    }

    [Fact]
    public void IsPassthrough_CreateWithBuffer_ReturnsFalse()
    {
        var ms = new MemoryStream(new byte[] { 1, 2, 3, 4, 5 });
        var nonSeekableMs = new ForwardOnlyStream(ms);
        var stream = SharpCompressStream.Create(nonSeekableMs, 128);
        Assert.False(stream.IsPassthrough);
    }

    [Fact]
    public void IsPassthrough_CreateSeekable_ReturnsFalse()
    {
        var ms = new MemoryStream(new byte[] { 1, 2, 3, 4, 5 });
        var stream = SharpCompressStream.Create(ms);
        Assert.False(stream.IsPassthrough);
    }

    [Fact]
    public void IsRecording_AfterStartRecording_ReturnsTrue()
    {
        var ms = new MemoryStream(new byte[] { 1, 2, 3, 4, 5 });
        var nonSeekableMs = new ForwardOnlyStream(ms);
        var stream = SharpCompressStream.Create(nonSeekableMs, 128);
        stream.StartRecording();
        Assert.True(stream.IsRecording);
    }

    [Fact]
    public void IsRecording_AfterStopRecording_ReturnsFalse()
    {
        var ms = new MemoryStream(new byte[] { 1, 2, 3, 4, 5 });
        var nonSeekableMs = new ForwardOnlyStream(ms);
        var stream = SharpCompressStream.Create(nonSeekableMs, 128);
        stream.StartRecording();
        stream.Read(new byte[4], 0, 4);
        stream.StopRecording();
        Assert.False(stream.IsRecording);
    }

    [Fact]
    public void IsRecording_AfterRewindWithStopRecording_ReturnsFalse()
    {
        var ms = new MemoryStream(new byte[] { 1, 2, 3, 4, 5 });
        var nonSeekableMs = new ForwardOnlyStream(ms);
        var stream = SharpCompressStream.Create(nonSeekableMs, 128);
        stream.StartRecording();
        stream.Read(new byte[4], 0, 4);
        stream.Rewind(true);
        Assert.False(stream.IsRecording);
    }

    [Fact]
    public void LeaveStreamOpen_CreateNonDisposing_ReturnsTrue()
    {
        var ms = new MemoryStream(new byte[] { 1, 2, 3, 4, 5 });
        var stream = SharpCompressStream.CreateNonDisposing(ms);
        Assert.True(stream.LeaveStreamOpen);
    }

    [Fact]
    public void LeaveStreamOpen_CreateWithBuffer_ReturnsFalse()
    {
        var ms = new MemoryStream(new byte[] { 1, 2, 3, 4, 5 });
        var nonSeekableMs = new ForwardOnlyStream(ms);
        var stream = SharpCompressStream.Create(nonSeekableMs, 128);
        Assert.False(stream.LeaveStreamOpen);
    }

    [Fact]
    public void LeaveStreamOpen_CreateSeekable_ReturnsFalse()
    {
        var ms = new MemoryStream(new byte[] { 1, 2, 3, 4, 5 });
        var stream = SharpCompressStream.Create(ms);
        Assert.False(stream.LeaveStreamOpen);
    }

    [Fact]
    public void CanRead_AlwaysTrue()
    {
        var ms = new MemoryStream(new byte[] { 1, 2, 3, 4, 5 });
        var stream = SharpCompressStream.CreateNonDisposing(ms);
        Assert.True(stream.CanRead);
    }

    [Fact]
    public void CanSeek_PassthroughWithSeekable_DelegatesTrue()
    {
        var ms = new MemoryStream(new byte[] { 1, 2, 3, 4, 5 });
        var stream = SharpCompressStream.CreateNonDisposing(ms);
        Assert.True(stream.CanSeek);
    }

    [Fact]
    public void CanSeek_PassthroughWithNonSeekable_DelegatesFalse()
    {
        var ms = new MemoryStream(new byte[] { 1, 2, 3, 4, 5 });
        var nonSeekableMs = new ForwardOnlyStream(ms);
        var stream = SharpCompressStream.CreateNonDisposing(nonSeekableMs);
        Assert.False(stream.CanSeek);
    }

    [Fact]
    public void CanWrite_PassthroughWithWritable_DelegatesTrue()
    {
        var ms = new MemoryStream();
        var stream = SharpCompressStream.CreateNonDisposing(ms);
        Assert.True(stream.CanWrite);
    }

    [Fact]
    public void CanWrite_PassthroughWithReadOnly_DelegatesFalse()
    {
        var ms = new MemoryStream(new byte[] { 1, 2, 3, 4, 5 });
        var readOnlyMs = new ReadOnlyStreamWrapper(ms);
        var stream = SharpCompressStream.CreateNonDisposing(readOnlyMs);
        Assert.False(stream.CanWrite);
    }

    private class ReadOnlyStreamWrapper : Stream
    {
        private readonly Stream _baseStream;

        public ReadOnlyStreamWrapper(Stream baseStream)
        {
            _baseStream = baseStream;
        }

        public override bool CanRead => _baseStream.CanRead;
        public override bool CanSeek => _baseStream.CanSeek;
        public override bool CanWrite => false;
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
            throw new NotSupportedException();
    }
}
