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

    [Fact]
    public void EnsureMinimumRewindBufferSize_ExpandsSmallBuffer_PreservesExistingData()
    {
        // Arrange: create a stream with a small initial buffer (size 10)
        var ms = new MemoryStream(new byte[] { 1, 2, 3, 4, 5, 6, 7, 8 });
        var nonSeekableMs = new NonSeekableStreamWrapper(ms);
        var stream = SharpCompressStream.Create(nonSeekableMs, 10);
        stream.StartRecording();

        // Read 4 bytes — they are now in the ring buffer
        var buffer = new byte[8];
        stream.Read(buffer, 0, 4);
        Assert.Equal(4, stream.Position);

        // Rewind to verify 4 bytes are present
        stream.Rewind();

        // Act: expand the ring buffer to 200 bytes while data is present
        stream.EnsureMinimumRewindBufferSize(200);

        // Verify the data is still replayable after expansion
        var readBuffer = new byte[4];
        stream.Read(readBuffer, 0, 4);
        Assert.Equal(1, readBuffer[0]);
        Assert.Equal(2, readBuffer[1]);
        Assert.Equal(3, readBuffer[2]);
        Assert.Equal(4, readBuffer[3]);
    }

    [Fact]
    public void EnsureMinimumRewindBufferSize_BufferAlreadyLarger_DoesNotShrink()
    {
        // Arrange: create a stream with a large initial buffer (size 200)
        var ms = new MemoryStream(new byte[] { 1, 2, 3, 4, 5 });
        var nonSeekableMs = new NonSeekableStreamWrapper(ms);
        var stream = SharpCompressStream.Create(nonSeekableMs, 200);
        stream.StartRecording();
        stream.Read(new byte[5], 0, 5);

        // Act: request a smaller minimum — buffer should stay at 200
        stream.EnsureMinimumRewindBufferSize(50);

        // Assert: buffer can still hold the 5 bytes written before expansion request
        stream.Rewind();
        var readBuffer = new byte[5];
        stream.Read(readBuffer, 0, 5);
        Assert.Equal(1, readBuffer[0]);
        Assert.Equal(5, readBuffer[4]);
    }

    [Fact]
    public void EnsureMinimumRewindBufferSize_AllowsRewindAfterLargeRead()
    {
        // Simulate the BZip2 scenario: small initial buffer, expand after format detection,
        // then verify a large read still allows Rewind.
        const int initialSize = 10;
        const int expandedSize = 100;
        const int largeReadSize = 80;

        var data = new byte[100];
        for (var i = 0; i < data.Length; i++)
        {
            data[i] = (byte)(i + 1);
        }

        var ms = new MemoryStream(data);
        var nonSeekableMs = new NonSeekableStreamWrapper(ms);
        var stream = SharpCompressStream.Create(nonSeekableMs, initialSize);
        stream.StartRecording();

        // Read 4 bytes (format detection — magic bytes)
        var buffer = new byte[4];
        stream.Read(buffer, 0, 4);
        stream.Rewind();

        // Expand the ring buffer to cover the anticipated large probe read
        stream.EnsureMinimumRewindBufferSize(expandedSize);

        // Read a large amount (simulating BZip2 block decompression)
        var largeBuffer = new byte[largeReadSize];
        stream.Read(largeBuffer, 0, largeReadSize);

        // Rewind must succeed even though largeReadSize > initialSize
        stream.Rewind();

        // Verify data replays correctly
        var verifyBuffer = new byte[largeReadSize];
        stream.Read(verifyBuffer, 0, largeReadSize);
        Assert.Equal(data[0], verifyBuffer[0]);
        Assert.Equal(data[largeReadSize - 1], verifyBuffer[largeReadSize - 1]);
    }
}
