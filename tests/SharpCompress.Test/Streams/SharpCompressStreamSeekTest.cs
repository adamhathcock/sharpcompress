using System;
using System.IO;
using SharpCompress.IO;
using SharpCompress.Test.Mocks;
using Xunit;

namespace SharpCompress.Test.Streams;

public class SharpCompressStreamSeekTest
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
    public void Seek_CurrentOrigin_MovesRelativeToCurrent()
    {
        var ms = new MemoryStream(new byte[] { 1, 2, 3, 4, 5, 6, 7, 8 });
        var nonSeekableMs = new NonSeekableStreamWrapper(ms);
        var stream = SharpCompressStream.Create(nonSeekableMs, 128);
        stream.StartRecording();
        var buffer = new byte[4];
        stream.Read(buffer, 0, 4);
        Assert.Equal(4, stream.Position);

        stream.Seek(-2, SeekOrigin.Current);
        Assert.Equal(2, stream.Position);

        stream.Read(buffer, 0, 2);
        Assert.Equal(3, buffer[0]);
        Assert.Equal(4, buffer[1]);
    }

    [Fact]
    public void Seek_BeginOrigin_MovesToAbsolutePosition()
    {
        var ms = new MemoryStream(new byte[] { 1, 2, 3, 4, 5, 6, 7, 8 });
        var nonSeekableMs = new NonSeekableStreamWrapper(ms);
        var stream = SharpCompressStream.Create(nonSeekableMs, 128);
        stream.StartRecording();
        var buffer = new byte[8];
        stream.Read(buffer, 0, 8);

        stream.Seek(2, SeekOrigin.Begin);
        Assert.Equal(2, stream.Position);

        var readBuffer = new byte[2];
        stream.Read(readBuffer, 0, 2);
        Assert.Equal(3, readBuffer[0]);
        Assert.Equal(4, readBuffer[1]);
    }

    [Fact]
    public void Seek_ToExactBufferBoundary_Succeeds()
    {
        var ms = new MemoryStream(new byte[] { 1, 2, 3, 4, 5, 6, 7, 8 });
        var nonSeekableMs = new NonSeekableStreamWrapper(ms);
        var stream = SharpCompressStream.Create(nonSeekableMs, 128);
        stream.StartRecording();
        var buffer = new byte[4];
        stream.Read(buffer, 0, 4);

        stream.Seek(4, SeekOrigin.Begin);
        Assert.Equal(4, stream.Position);

        stream.Read(buffer, 0, 4);
        Assert.Equal(5, buffer[0]);
        Assert.Equal(6, buffer[1]);
        Assert.Equal(7, buffer[2]);
        Assert.Equal(8, buffer[3]);
    }

    [Fact]
    public void Position_SetWithinRecordedRange_Succeeds()
    {
        var ms = new MemoryStream(new byte[] { 1, 2, 3, 4, 5, 6, 7, 8 });
        var nonSeekableMs = new NonSeekableStreamWrapper(ms);
        var stream = SharpCompressStream.Create(nonSeekableMs, 128);
        stream.StartRecording();
        var buffer = new byte[8];
        stream.Read(buffer, 0, 8);

        stream.Position = 2;
        Assert.Equal(2, stream.Position);

        var readBuffer = new byte[2];
        stream.Read(readBuffer, 0, 2);
        Assert.Equal(3, readBuffer[0]);
        Assert.Equal(4, readBuffer[1]);
    }
}
