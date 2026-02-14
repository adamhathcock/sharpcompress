using System;
using System.IO;
using System.Threading.Tasks;
using SharpCompress.IO;
using Xunit;

namespace SharpCompress.Test.Streams;

public class SharpCompressStreamErrorAsyncTest
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

#if !LEGACY_DOTNET
    [Fact]
    public async ValueTask DisposeAsync_WithThrowOnDisposeTrue_ThrowsInvalidOperation()
    {
        var ms = new MemoryStream(new byte[] { 1, 2, 3, 4, 5 });
        var stream = SharpCompressStream.CreateNonDisposing(ms);
        stream.ThrowOnDispose = true;
        await Assert
            .ThrowsAsync<InvalidOperationException>(async () =>
                await stream.DisposeAsync().ConfigureAwait(false)
            )
            .ConfigureAwait(false);
    }
#endif

    [Fact]
    public async ValueTask CreateNonDisposing_ReadAsync_ZeroCount_ReturnsZero()
    {
        var ms = new MemoryStream(new byte[] { 1, 2, 3, 4, 5 });
        var stream = SharpCompressStream.CreateNonDisposing(ms);
        var buffer = new byte[10];
        int bytesRead = await stream.ReadAsync(buffer, 0, 0).ConfigureAwait(false);
        Assert.Equal(0, bytesRead);
    }

    [Fact]
    public async ValueTask CreateNonDisposing_ReadAsync_AtEndOfStream_ReturnsZero()
    {
        var ms = new MemoryStream(new byte[] { 1, 2, 3, 4, 5 });
        var stream = SharpCompressStream.CreateNonDisposing(ms);
        var buffer = new byte[10];
        await stream.ReadAsync(buffer, 0, buffer.Length).ConfigureAwait(false);
        int bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length).ConfigureAwait(false);
        Assert.Equal(0, bytesRead);
    }

    [Fact]
    public async ValueTask Create_AsyncReadWithRecording_WorksCorrectly()
    {
        var ms = new MemoryStream(new byte[] { 1, 2, 3, 4, 5 });
        var nonSeekableMs = new NonSeekableStreamWrapper(ms);
        var stream = SharpCompressStream.Create(nonSeekableMs, 128);
        stream.StartRecording();
        var buffer = new byte[4];
        await stream.ReadAsync(buffer, 0, 4).ConfigureAwait(false);
        Assert.Equal(4, stream.Position);
    }

    [Fact]
    public async ValueTask Create_AsyncReadWithBufferOverflow_ThrowsInvalidOperation()
    {
        var ms = new MemoryStream(new byte[256]);
        var nonSeekableMs = new NonSeekableStreamWrapper(ms);
        var stream = SharpCompressStream.Create(nonSeekableMs, 64);
        stream.StartRecording();
        var buffer = new byte[32];
        for (int i = 0; i < 3; i++)
        {
            await stream.ReadExactAsync(buffer, 0, 32).ConfigureAwait(false);
        }
        Assert.Throws<InvalidOperationException>(() => stream.Rewind());
    }

    [Fact]
    public async ValueTask FlushAsync_NonPassthrough_ThrowsNotSupported()
    {
        var ms = new MemoryStream(new byte[] { 1, 2, 3, 4, 5 });
        var nonSeekableMs = new NonSeekableStreamWrapper(ms);
        var stream = SharpCompressStream.Create(nonSeekableMs, 128);
        await Assert
            .ThrowsAsync<NotSupportedException>(async () =>
                await stream.FlushAsync().ConfigureAwait(false)
            )
            .ConfigureAwait(false);
    }

    [Fact]
    public async ValueTask WriteAsync_NonPassthrough_ThrowsNotSupported()
    {
        var ms = new MemoryStream(new byte[] { 1, 2, 3, 4, 5 });
        var nonSeekableMs = new NonSeekableStreamWrapper(ms);
        var stream = SharpCompressStream.Create(nonSeekableMs, 128);
        await Assert
            .ThrowsAsync<NotSupportedException>(async () =>
                await stream.WriteAsync(new byte[] { 1 }, 0, 1).ConfigureAwait(false)
            )
            .ConfigureAwait(false);
    }

    [Fact]
    public async ValueTask CopyToAsync_WithPassthrough_CopiesAllData()
    {
        var ms = new MemoryStream(new byte[] { 1, 2, 3, 4, 5 });
        var stream = SharpCompressStream.CreateNonDisposing(ms);
        var destination = new MemoryStream();
        await stream.CopyToAsync(destination).ConfigureAwait(false);
        Assert.Equal(new byte[] { 1, 2, 3, 4, 5 }, destination.ToArray());
    }
}
