using System;
using System.IO;
using SharpCompress.Common;
using SharpCompress.IO;
using SharpCompress.Test.Mocks;
using Xunit;

namespace SharpCompress.Test.Streams;

public class SharpCompressStreamErrorTest
{
    private class NonSeekableStreamWrapper : Stream
    {
        private readonly Stream _baseStream;

        public NonSeekableStreamWrapper(Stream baseStream)
        {
            _baseStream = baseStream;
        }

        public override bool CanRead => _baseStream.CanRead;

        public override bool CanSeek => false;

        public override bool CanWrite => _baseStream.CanWrite;

        public override long Length => _baseStream.Length;

        public override long Position
        {
            get => throw new NotSupportedException();
            set => throw new NotSupportedException();
        }

        public override void Flush() => _baseStream.Flush();

        public override int Read(byte[] buffer, int offset, int count) =>
            _baseStream.Read(buffer, offset, count);

        public override long Seek(long offset, SeekOrigin origin) =>
            throw new NotSupportedException();

        public override void SetLength(long value) => _baseStream.SetLength(value);

        public override void Write(byte[] buffer, int offset, int count) =>
            _baseStream.Write(buffer, offset, count);

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _baseStream.Dispose();
            }
            base.Dispose(disposing);
        }
    }

    [Fact]
    public void Rewind_WithoutStartRecording_ThrowsInvalidOperation()
    {
        var ms = new MemoryStream(new byte[] { 1, 2, 3, 4, 5 });
        var nonSeekableMs = new NonSeekableStreamWrapper(ms);
        var stream = SharpCompressStream.Create(nonSeekableMs, 128);
        Assert.Throws<ArchiveOperationException>(() => stream.Rewind());
    }

    [Fact]
    public void Rewind_PassthroughMode_ThrowsInvalidOperation()
    {
        var ms = new MemoryStream(new byte[] { 1, 2, 3, 4, 5 });
        var stream = SharpCompressStream.CreateNonDisposing(ms);
        Assert.Throws<ArchiveOperationException>(() => stream.Rewind());
    }

    [Fact]
    public void StartRecording_Twice_ThrowsInvalidOperation()
    {
        var ms = new MemoryStream(new byte[] { 1, 2, 3, 4, 5 });
        var nonSeekableMs = new NonSeekableStreamWrapper(ms);
        var stream = SharpCompressStream.Create(nonSeekableMs, 128);
        stream.StartRecording();
        Assert.Throws<ArchiveOperationException>(() => stream.StartRecording());
    }

    [Fact]
    public void StartRecording_PassthroughMode_ThrowsInvalidOperation()
    {
        var ms = new MemoryStream(new byte[] { 1, 2, 3, 4, 5 });
        var stream = SharpCompressStream.CreateNonDisposing(ms);
        Assert.Throws<ArchiveOperationException>(() => stream.StartRecording());
    }

    [Fact]
    public void StopRecording_WithoutRecording_ThrowsInvalidOperation()
    {
        var ms = new MemoryStream(new byte[] { 1, 2, 3, 4, 5 });
        var nonSeekableMs = new NonSeekableStreamWrapper(ms);
        var stream = SharpCompressStream.Create(nonSeekableMs, 128);
        Assert.Throws<ArchiveOperationException>(() => stream.StopRecording());
    }

    [Fact]
    public void StopRecording_PassthroughMode_ThrowsInvalidOperation()
    {
        var ms = new MemoryStream(new byte[] { 1, 2, 3, 4, 5 });
        var stream = SharpCompressStream.CreateNonDisposing(ms);
        Assert.Throws<ArchiveOperationException>(() => stream.StopRecording());
    }

    [Fact]
    public void StopRecording_Twice_ThrowsInvalidOperation()
    {
        var ms = new MemoryStream(new byte[] { 1, 2, 3, 4, 5 });
        var nonSeekableMs = new NonSeekableStreamWrapper(ms);
        var stream = SharpCompressStream.Create(nonSeekableMs, 128);
        stream.StartRecording();
        stream.Read(new byte[4], 0, 4);
        stream.StopRecording();
        Assert.Throws<ArchiveOperationException>(() => stream.StopRecording());
    }

    [Fact]
    public void Seek_BeyondRecordedRange_ThrowsNotSupported()
    {
        var ms = new MemoryStream(new byte[] { 1, 2, 3, 4, 5 });
        var nonSeekableMs = new NonSeekableStreamWrapper(ms);
        var stream = SharpCompressStream.Create(nonSeekableMs, 128);
        stream.StartRecording();
        stream.Read(new byte[4], 0, 4);
        Assert.Throws<NotSupportedException>(() => stream.Position = 100);
    }

    [Fact]
    public void Seek_FromEnd_ThrowsNotSupported()
    {
        var ms = new MemoryStream(new byte[] { 1, 2, 3, 4, 5 });
        var nonSeekableMs = new NonSeekableStreamWrapper(ms);
        var stream = SharpCompressStream.Create(nonSeekableMs, 128);
        Assert.Throws<NotSupportedException>(() => stream.Seek(-1, SeekOrigin.End));
    }

    [Fact]
    public void Position_SetNegative_Throws()
    {
        var ms = new MemoryStream(new byte[] { 1, 2, 3, 4, 5 });
        var nonSeekableMs = new NonSeekableStreamWrapper(ms);
        var stream = SharpCompressStream.Create(nonSeekableMs, 128);
        stream.StartRecording();
        stream.Read(new byte[4], 0, 4);
        Assert.Throws<NotSupportedException>(() => stream.Position = -1);
    }

    [Fact]
    public void Flush_NonPassthrough_ThrowsNotSupported()
    {
        var ms = new MemoryStream(new byte[] { 1, 2, 3, 4, 5 });
        var nonSeekableMs = new NonSeekableStreamWrapper(ms);
        var stream = SharpCompressStream.Create(nonSeekableMs, 128);
        Assert.Throws<NotSupportedException>(() => stream.Flush());
    }

    [Fact]
    public void Write_NonPassthrough_ThrowsNotSupported()
    {
        var ms = new MemoryStream(new byte[] { 1, 2, 3, 4, 5 });
        var nonSeekableMs = new NonSeekableStreamWrapper(ms);
        var stream = SharpCompressStream.Create(nonSeekableMs, 128);
        Assert.Throws<NotSupportedException>(() => stream.Write(new byte[] { 1 }, 0, 1));
    }

    [Fact]
    public void SetLength_NonPassthrough_ThrowsNotSupported()
    {
        var ms = new MemoryStream(new byte[] { 1, 2, 3, 4, 5 });
        var nonSeekableMs = new NonSeekableStreamWrapper(ms);
        var stream = SharpCompressStream.Create(nonSeekableMs, 128);
        Assert.Throws<NotSupportedException>(() => stream.SetLength(100));
    }

    [Fact]
    public void Length_NonPassthroughWithoutBuffer_ThrowsNotSupported()
    {
        var ms = new MemoryStream(new byte[] { 1, 2, 3, 4, 5 });
        var nonSeekableMs = new ForwardOnlyStream(ms);
        var stream = new SharpCompressStream(nonSeekableMs);
        Assert.Throws<NotSupportedException>(() => stream.Length);
    }
}
