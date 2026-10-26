using System;
using System.Buffers;
using System.IO;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using SharpCompress.Compressors.LZMA;
using Xunit;

namespace SharpCompress.Test.Streams;

public class AesEncoderStreamTests
{
    private static readonly byte[] Key = new byte[32];
    private static readonly byte[] Iv = new byte[16];

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(15)]
    [InlineData(16)]
    [InlineData(17)]
    [InlineData(4095)]
    [InlineData(4096)]
    [InlineData(10003)]
    public async Task MixedWritesMatchCbcAndReturnOversizedLease(int length)
    {
        var input = new byte[length];
        new Random(1476).NextBytes(input);
        var padded = new byte[(length + 15) & ~15];
        input.CopyTo(padded, 0);
        using var aes = Aes.Create();
        aes.Padding = PaddingMode.None;
        using var transform = aes.CreateEncryptor(Key, Iv);
        var expected = transform.TransformFinalBlock(padded, 0, padded.Length);
        var pool = new CountingPool();
        using var output = new RecordingOutput();
        var encoder = new AesEncoderStream(output, Key, Iv, pool);
        var offset = 0;
        if (length > 0)
        {
            encoder.WriteByte(input[offset++]);
        }
        var count = Math.Min(13, length - offset);
        encoder.Write(input, offset, count);
        offset += count;
        count = Math.Max(0, length - offset - 1);
        await encoder.WriteAsync(input, offset, count, CancellationToken.None);
        offset += count;
        if (offset < length)
        {
            encoder.WriteByte(input[offset]);
        }
        Assert.Equal(length, encoder.InputLength);
        await encoder.FinishAsync(CancellationToken.None);
        encoder.Finish();
        Assert.Equal(expected, output.ToArray());
        Assert.InRange(output.MaxWrite, 0, 4096);
        Assert.Equal(0, pool.Returns);
#if !LEGACY_DOTNET
        await encoder.DisposeAsync();
#else
        encoder.Dispose();
#endif
        encoder.Dispose();
        Assert.Equal(1, pool.Returns);
        Assert.True(output.CanWrite);
        Assert.False(encoder.CanWrite);
        Assert.Throws<ObjectDisposedException>(() => encoder.WriteByte(0));
        Assert.Throws<ObjectDisposedException>(() => encoder.Write(input, 0, 0));
        Assert.Throws<ObjectDisposedException>(() => encoder.Finish());
        await Assert.ThrowsAsync<ObjectDisposedException>(() =>
            encoder.WriteAsync(input, 0, 0, CancellationToken.None)
        );
        await Assert.ThrowsAsync<ObjectDisposedException>(async () =>
            await encoder.FinishAsync(CancellationToken.None)
        );
    }

    [Fact]
    public void ByteWritesMatchBulkWrites()
    {
        var input = new byte[10003];
        new Random(42).NextBytes(input);
        using var bulk = new MemoryStream();
        using var bytes = new MemoryStream();
        using var first = new AesEncoderStream(bulk, Key, Iv);
        using var second = new AesEncoderStream(bytes, Key, Iv);
        first.Write(input, 0, input.Length);
        foreach (var value in input)
        {
            second.WriteByte(value);
        }
        first.Finish();
        second.Finish();
        Assert.Equal(bulk.ToArray(), bytes.ToArray());
        Assert.Throws<ObjectDisposedException>(() => second.WriteByte(0));
    }

    [Fact]
    public void FailedCryptoSetupDoesNotRent()
    {
        var pool = new CountingPool();
        var exception = Record.Exception(() =>
            new AesEncoderStream(Stream.Null, new byte[1], Iv, pool)
        );
        Assert.True(
            exception is ArgumentException or System.Security.Cryptography.CryptographicException
        );
        Assert.Equal(0, pool.Rents);
    }

    [Fact]
    public void EarlyDisposalReturnsLeaseWithoutWritingPendingData()
    {
        var pool = new CountingPool();
        using var output = new MemoryStream();
        using (var encoder = new AesEncoderStream(output, Key, Iv, pool))
        {
            encoder.WriteByte(42);
        }
        Assert.Equal(0, output.Length);
        Assert.Equal(1, pool.Returns);
        Assert.True(output.CanWrite);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task FailedOrCanceledOutputReturnsLeaseOnDisposal(bool canceled)
    {
        var pool = new CountingPool();
        using var output = new FailingOutput();
        var encoder = new AesEncoderStream(output, Key, Iv, pool);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        if (canceled)
        {
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
                encoder.WriteAsync(new byte[16], 0, 16, cancellation.Token)
            );
        }
        else
        {
            Assert.Throws<IOException>(() => encoder.Write(new byte[16], 0, 16));
        }
        encoder.Dispose();
        encoder.Dispose();
        Assert.Equal(1, pool.Returns);
        Assert.True(output.CanWrite);
    }

#if !LEGACY_DOTNET && !DEBUG
    [Fact]
    public void ByteWritesDoNotAllocatePerByte()
    {
        using var encoder = new AesEncoderStream(Stream.Null, Key, Iv);
        for (var i = 0; i < 1024; i++)
        {
            encoder.WriteByte((byte)i);
        }
        var start = GC.GetAllocatedBytesForCurrentThread();
        for (var i = 0; i < 1024 * 1024; i++)
        {
            encoder.WriteByte((byte)i);
        }
        encoder.Finish();
        Assert.InRange(GC.GetAllocatedBytesForCurrentThread() - start, 0, 65536);
    }

    [Fact]
    public void RepeatedConstructionReusesScratchBuffer()
    {
        using (var warmup = new AesEncoderStream(Stream.Null, Key, Iv)) { }
        var start = GC.GetAllocatedBytesForCurrentThread();
        for (var i = 0; i < 100; i++)
        {
            using var encoder = new AesEncoderStream(Stream.Null, Key, Iv);
        }
        Assert.InRange(GC.GetAllocatedBytesForCurrentThread() - start, 0, 200000);
    }
#endif

    private sealed class CountingPool : ArrayPool<byte>
    {
        private byte[]? lease;
        public int Rents { get; private set; }
        public int Returns { get; private set; }

        public override byte[] Rent(int minimumLength)
        {
            Rents++;
            // Deliberately not block-aligned: capacity must not control encryption batches.
            lease = new byte[minimumLength + 37];
            for (var i = 0; i < lease.Length; i++)
            {
                lease[i] = 0xCC;
            }
            return lease;
        }

        public override void Return(byte[] array, bool clearArray = false)
        {
            Assert.Same(lease, array);
            Returns++;
        }
    }

    private sealed class RecordingOutput : MemoryStream
    {
        public int MaxWrite { get; private set; }

        public override void Write(byte[] buffer, int offset, int count)
        {
            MaxWrite = Math.Max(MaxWrite, count);
            base.Write(buffer, offset, count);
        }
    }

    private sealed class FailingOutput : MemoryStream
    {
        public override void Write(byte[] buffer, int offset, int count) =>
            throw new IOException("Injected output failure");

        public override Task WriteAsync(
            byte[] buffer,
            int offset,
            int count,
            CancellationToken token
        ) =>
            token.IsCancellationRequested
                ? Task.FromCanceled(token)
                : Task.FromException(new IOException("Injected output failure"));
    }
}
