using System;
using System.IO;
using System.Threading.Tasks;
using SharpCompress.IO;
using Xunit;

namespace SharpCompress.Test.Streams;

public class SharpCompressStreamSeekAsyncTest
{
    private class NonSeekableStreamWrapper : Stream
    {
        private readonly Stream _baseStream;

        public NonSeekableStreamWrapper(Stream baseStream) => _baseStream = baseStream;

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
    public async ValueTask SeekAsync_AfterReadAsync_MaintainsPosition()
    {
        var ms = new MemoryStream(new byte[] { 1, 2, 3, 4, 5, 6, 7, 8 });
        var nonSeekableMs = new NonSeekableStreamWrapper(ms);
        var stream = SharpCompressStream.Create(nonSeekableMs, 128);
        stream.StartRecording();
        var buffer = new byte[4];
        await stream.ReadAsync(buffer, 0, 4).ConfigureAwait(false);
        Assert.Equal(4, stream.Position);

        stream.Seek(-2, SeekOrigin.Current);
        Assert.Equal(2, stream.Position);

        await stream.ReadAsync(buffer, 0, 2).ConfigureAwait(false);
        Assert.Equal(3, buffer[0]);
        Assert.Equal(4, buffer[1]);
    }

    [Fact]
    public async ValueTask Position_Set_AfterAsyncRead_WorksCorrectly()
    {
        var ms = new MemoryStream(new byte[] { 1, 2, 3, 4, 5, 6, 7, 8 });
        var nonSeekableMs = new NonSeekableStreamWrapper(ms);
        var stream = SharpCompressStream.Create(nonSeekableMs, 128);
        stream.StartRecording();
        var buffer = new byte[8];
        await stream.ReadAsync(buffer, 0, 8).ConfigureAwait(false);

        stream.Position = 2;
        Assert.Equal(2, stream.Position);

        var readBuffer = new byte[2];
        await stream.ReadAsync(readBuffer, 0, 2).ConfigureAwait(false);
        Assert.Equal(3, readBuffer[0]);
        Assert.Equal(4, readBuffer[1]);
    }

    [Fact]
    public async ValueTask SeekAsync_ToRecordingStart_AfterAsyncRead_WorksCorrectly()
    {
        var ms = new MemoryStream(new byte[] { 1, 2, 3, 4, 5, 6, 7, 8 });
        var nonSeekableMs = new NonSeekableStreamWrapper(ms);
        var stream = SharpCompressStream.Create(nonSeekableMs, 128);
        stream.StartRecording();
        var buffer = new byte[4];
        await stream.ReadAsync(buffer, 0, 4).ConfigureAwait(false);

        stream.Position = 0;
        Assert.Equal(0, stream.Position);

        await stream.ReadAsync(buffer, 0, 4).ConfigureAwait(false);
        Assert.Equal(1, buffer[0]);
        Assert.Equal(2, buffer[1]);
        Assert.Equal(3, buffer[2]);
        Assert.Equal(4, buffer[3]);
    }

    [Fact]
    public async ValueTask SeekAsync_ZeroCurrentOrigin_DoesNotMove()
    {
        var ms = new MemoryStream(new byte[] { 1, 2, 3, 4, 5, 6, 7, 8 });
        var nonSeekableMs = new NonSeekableStreamWrapper(ms);
        var stream = SharpCompressStream.Create(nonSeekableMs, 128);
        stream.StartRecording();
        var buffer = new byte[4];
        await stream.ReadAsync(buffer, 0, 4).ConfigureAwait(false);
        Assert.Equal(4, stream.Position);

        stream.Seek(0, SeekOrigin.Current);
        Assert.Equal(4, stream.Position);
    }

    [Fact]
    public async ValueTask SeekAsync_NegativeCurrent_MovesBackward()
    {
        var ms = new MemoryStream(new byte[] { 1, 2, 3, 4, 5, 6, 7, 8 });
        var nonSeekableMs = new NonSeekableStreamWrapper(ms);
        var stream = SharpCompressStream.Create(nonSeekableMs, 128);
        stream.StartRecording();
        var buffer = new byte[6];
        await stream.ReadAsync(buffer, 0, 6).ConfigureAwait(false);
        Assert.Equal(6, stream.Position);

        stream.Seek(-3, SeekOrigin.Current);
        Assert.Equal(3, stream.Position);

        var readBuffer = new byte[3];
        await stream.ReadAsync(readBuffer, 0, 3).ConfigureAwait(false);
        Assert.Equal(4, readBuffer[0]);
        Assert.Equal(5, readBuffer[1]);
        Assert.Equal(6, readBuffer[2]);
    }
}
