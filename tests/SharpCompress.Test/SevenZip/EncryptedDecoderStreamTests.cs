using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using SharpCompress.Common;
using SharpCompress.Compressors.LZMA;
using Xunit;

namespace SharpCompress.Test.SevenZip;

public class EncryptedDecoderStreamTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task EncryptedDecoderStream_DataError_PreservesInnerException(bool async)
    {
        var error = new DataErrorException();
        using var decoder = new FailingStream(error);
        using var stream = new EncryptedDecoderStream(decoder);
        var exception = await Assert.ThrowsAsync<CryptographicException>(async () =>
        {
            if (async)
            {
                await stream.ReadAsync(new byte[1], 0, 1, CancellationToken.None);
            }
            else
            {
                stream.Read(new byte[1], 0, 1);
            }
        });
        Assert.Same(error, exception.InnerException);
    }

    [Fact]
    public async Task EncryptedDecoderStream_OtherErrors_AreNotTranslated()
    {
        var error = new IOException("read failed");
        using var decoder = new FailingStream(error);
        using var stream = new EncryptedDecoderStream(decoder);
        Assert.Same(error, Assert.Throws<IOException>(() => stream.ReadByte()));
        Assert.Same(
            error,
            await Assert.ThrowsAsync<IOException>(() => stream.ReadAsync(new byte[1], 0, 1))
        );
        using var canceled = new CancellationTokenSource();
        canceled.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            stream.ReadAsync(new byte[1], 0, 1, canceled.Token)
        );
        stream.Dispose();
        Assert.True(decoder.Disposed);
    }

    private sealed class FailingStream(Exception exception) : MemoryStream
    {
        public bool Disposed { get; private set; }

        public override int Read(byte[] buffer, int offset, int count) => throw exception;

        public override Task<int> ReadAsync(
            byte[] buffer,
            int offset,
            int count,
            CancellationToken cancellationToken
        )
        {
            cancellationToken.ThrowIfCancellationRequested();
            throw exception;
        }

        protected override void Dispose(bool disposing)
        {
            Disposed = true;
            base.Dispose(disposing);
        }
    }
}
