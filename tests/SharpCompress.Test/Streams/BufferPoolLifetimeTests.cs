using System;
using System.Buffers;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using SharpCompress.Common;
using SharpCompress.Common.Tar.Headers;
using SharpCompress.Compressors.LZMA;
using SharpCompress.Compressors.LZMA.LZ;
using SharpCompress.Compressors.LZMA.Utilities;
using SharpCompress.IO;
using Xunit;

namespace SharpCompress.Test.Streams;

public class BufferPoolLifetimeTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task OutWindow_FailedFlush_ReturnsBuffer(bool asyncDispose)
    {
        var pool = new TrackingPool<byte>();
        using var output = new ThrowingOutputStream();
        var window = new OutWindow(pool);
        window.Create(32);
        window.Init(output);
        window.SetLimit(1);
        window.PutByte(42);
        if (asyncDispose)
        {
            await Assert.ThrowsAsync<IOException>(async () => await window.DisposeAsync());
        }
        else
        {
            Assert.Throws<IOException>(() => window.Dispose());
        }
        window.Dispose();
        Assert.Equal(1, pool.ReturnCount);
        Assert.Empty(pool.Active);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task CrcCheckStream_Dispose_ReturnsBufferOnce(bool invalidCrc)
    {
        var pool = new TrackingPool<long>();
        var stream = new CrcCheckStream(invalidCrc ? 1u : 0u, pool);

        if (invalidCrc)
        {
            Assert.Throws<ArchiveOperationException>(() => stream.Dispose());
        }
        else
        {
            stream.Dispose();
        }
        stream.Dispose();
#if !LEGACY_DOTNET
        await stream.DisposeAsync();
#else
        await Task.CompletedTask;
#endif

        Assert.Equal(1, pool.ReturnCount);
        Assert.Empty(pool.Active);
        Assert.False(stream.CanWrite);
        Assert.Throws<ObjectDisposedException>(() => stream.Write(new byte[] { 1 }, 0, 1));
        await Assert.ThrowsAsync<ObjectDisposedException>(() =>
            stream.WriteAsync(new byte[] { 1 }, 0, 1, CancellationToken.None)
        );
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task OutWindow_FailedGrowth_RetainsOwnership(bool asyncDispose)
    {
        var pool = new TrackingPool<byte>();
        var window = new OutWindow(pool);
        window.Create(32);
        window.PutByte(42);
        pool.FailNextRent = true;

        Assert.Throws<OutOfMemoryException>(() => window.Create(64));
        Assert.Equal(0, pool.ReturnCount);
        Assert.Equal(42, window.GetByte(0));

        if (asyncDispose)
        {
            await window.DisposeAsync();
        }
        else
        {
            window.Dispose();
        }
        window.Dispose();
        Assert.Equal(1, pool.ReturnCount);
        Assert.Empty(pool.Active);
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task OutWindow_LazyGrowth_PreservesPoolOwnership(bool asyncWrite, bool failGrowth)
    {
        var pool = new TrackingPool<byte>();
        using var window = new OutWindow(pool);
        window.Create(16384);
        Assert.Equal(4096, window.BufferCapacity);

        for (var i = 0; i < 4096; i++)
        {
            if (asyncWrite)
            {
                await window.PutByteAsync(42, CancellationToken.None);
            }
            else
            {
                window.PutByte(42);
            }
        }

        if (failGrowth)
        {
            pool.FailNextRent = true;
            if (asyncWrite)
            {
                await Assert.ThrowsAsync<OutOfMemoryException>(async () =>
                    await window.PutByteAsync(43, CancellationToken.None)
                );
            }
            else
            {
                Assert.Throws<OutOfMemoryException>(() => window.PutByte(43));
            }
            Assert.Equal(0, pool.ReturnCount);
            Assert.Equal(4096, window.BufferCapacity);
            Assert.Equal(42, window.GetByte(0));
        }

        if (asyncWrite)
        {
            await window.PutByteAsync(43, CancellationToken.None);
        }
        else
        {
            window.PutByte(43);
        }
        Assert.Equal(8192, window.BufferCapacity);
        Assert.Equal(42, window.GetByte(4096));
        Assert.Equal(43, window.GetByte(0));
        Assert.Equal(1, pool.ReturnCount);
        Assert.Single(pool.Active);

        if (asyncWrite)
        {
            await window.DisposeAsync();
        }
        else
        {
            window.Dispose();
        }
        Assert.Equal(2, pool.ReturnCount);
        Assert.Empty(pool.Active);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void BinTree_FailedGrowth_RetainsOwnership(bool growHash)
    {
        var pool = new TrackingPool<uint>();
        var tree = new BinTree(pool);
        if (growHash)
        {
            tree.SetType(2);
        }
        tree.Create(32, 0, 16, 0);
        if (growHash)
        {
            tree.SetType(4);
        }
        pool.FailNextRent = true;

        Assert.Throws<OutOfMemoryException>(() => tree.Create(growHash ? 32u : 64u, 0, 16, 0));
        Assert.Equal(0, pool.ReturnCount);
        tree.Dispose();
        tree.Dispose();
        Assert.Equal(2, pool.ReturnCount);
        Assert.Empty(pool.Active);
    }

    [Theory]
    [InlineData(0, 32)]
    [InlineData(-1, 32)]
    [InlineData(1073741825, 32)]
    [InlineData(65536, 4)]
    [InlineData(65536, 274)]
    public void Lzma2Encoder_InvalidProperties_DoesNotRentBuffer(
        int dictionarySize,
        int numFastBytes
    )
    {
        var pool = new TrackingPool<byte>();
        using var output = new MemoryStream();

        Assert.Throws<InvalidParamException>(() =>
            new Lzma2EncoderStream(output, dictionarySize, numFastBytes, pool)
        );
        Assert.Equal(0, pool.RentCount);
        Assert.Empty(pool.Active);
        Assert.True(output.CanWrite);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Lzma2Encoder_Dispose_RejectsWrites(bool asyncDispose)
    {
        var pool = new TrackingPool<byte>();
        using var output = new MemoryStream();
        var encoder = new Lzma2EncoderStream(output, 1 << 16, 32, pool);
        Assert.True(encoder.CanWrite);
        encoder.Write(new byte[128], 0, 128);

        if (asyncDispose)
        {
            await encoder.DisposeAsync();
        }
        else
        {
            encoder.Dispose();
        }

        var length = output.Length;
        await AssertWritesRejected(encoder);
        Assert.Equal(length, output.Length);
        Assert.Equal(1, pool.ReturnCount);
        Assert.Empty(pool.Active);
        Assert.True(output.CanWrite);
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task Lzma2Encoder_FailedDispose_ReturnsBufferOnce(bool asyncDispose, bool hasData)
    {
        var pool = new TrackingPool<byte>();
        using var output = new ThrowingOutputStream();
        var encoder = new Lzma2EncoderStream(output, 1 << 16, 32, pool);
        if (hasData)
        {
            encoder.Write(new byte[128], 0, 128);
        }

        if (asyncDispose)
        {
            await Assert.ThrowsAsync<IOException>(async () => await encoder.DisposeAsync());
        }
        else
        {
            Assert.Throws<IOException>(() => encoder.Dispose());
        }
        encoder.Dispose();
        await encoder.DisposeAsync();
        Assert.Equal(1, pool.ReturnCount);
        Assert.Empty(pool.Active);
        Assert.True(output.CanWrite);
        await AssertWritesRejected(encoder);
    }

    private static async Task AssertWritesRejected(Lzma2EncoderStream encoder)
    {
        Assert.False(encoder.CanWrite);
        foreach (var count in new[] { 0, 1 })
        {
            var data = new byte[count];
            Assert.Throws<ObjectDisposedException>(() => encoder.Write(data, 0, count));
            await Assert.ThrowsAsync<ObjectDisposedException>(() =>
                encoder.WriteAsync(data, 0, count, CancellationToken.None)
            );
#if !LEGACY_DOTNET
            await Assert.ThrowsAsync<ObjectDisposedException>(async () =>
                await encoder.WriteAsync(data.AsMemory(), CancellationToken.None)
            );
            Assert.Throws<ObjectDisposedException>(() => encoder.Write(data.AsSpan()));
#endif
        }
        Assert.Throws<ObjectDisposedException>(() => encoder.WriteByte(1));
    }

    [Fact]
    public async Task TarHeader_ReadAsync_ParsesBeforeReturningBuffer()
    {
        var pool = new TrackingPool<byte>();
        using var source = new MemoryStream();
        var expected = new TarHeader(new ArchiveEncoding())
        {
            Name = "pooled-header.txt",
            Size = 123,
            EntryType = EntryType.File,
            LastModifiedTime = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
        };
        expected.Write(source);
        source.Position = 0;
        using var reader = new AsyncBinaryReader(source, leaveOpen: true);
        var actual = new TarHeader(new ArchiveEncoding());

        Assert.True(await actual.ReadAsync(reader, null, pool, CancellationToken.None));
        Assert.Equal(expected.Name, actual.Name);
        Assert.Equal(expected.Size, actual.Size);
        Assert.Equal(expected.EntryType, actual.EntryType);
        Assert.Equal(expected.LastModifiedTime, actual.LastModifiedTime.ToUniversalTime());
        Assert.Equal(1, pool.ReturnCount);
        Assert.Empty(pool.Active);
        Assert.True(source.CanRead);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task TarHeader_ReadAsync_FailedRead_ReturnsBuffer(bool canceled)
    {
        var pool = new TrackingPool<byte>();
        using var source = new MemoryStream(new byte[10]);
        using var reader = new AsyncBinaryReader(source, leaveOpen: true);
        using var cancellation = new CancellationTokenSource();
        var header = new TarHeader(new ArchiveEncoding());
        if (canceled)
        {
            cancellation.Cancel();
            await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
                await header.ReadAsync(reader, null, pool, cancellation.Token)
            );
        }
        else
        {
            await Assert.ThrowsAsync<IncompleteArchiveException>(async () =>
                await header.ReadAsync(reader, null, pool, cancellation.Token)
            );
        }
        Assert.Equal(1, pool.ReturnCount);
        Assert.Empty(pool.Active);
        Assert.True(source.CanRead);
    }

    [Fact]
    public async Task TarHeader_ReadAsync_InvalidChecksum_ReturnsBuffer()
    {
        var pool = new TrackingPool<byte>();
        using var source = new MemoryStream(new byte[512]);
        using var reader = new AsyncBinaryReader(source, leaveOpen: true);
        var header = new TarHeader(new ArchiveEncoding());

        Assert.False(await header.ReadAsync(reader, null, pool, CancellationToken.None));
        Assert.Equal(1, pool.ReturnCount);
        Assert.Empty(pool.Active);
    }

    private sealed class TrackingPool<T> : ArrayPool<T>
    {
        internal HashSet<T[]> Active { get; } = new();
        internal int RentCount { get; private set; }
        internal int ReturnCount { get; private set; }
        internal bool FailNextRent { get; set; }

        public override T[] Rent(int minimumLength)
        {
            RentCount++;
            if (FailNextRent)
            {
                FailNextRent = false;
#pragma warning disable CA2201 // Simulate allocation failure without exhausting process memory.
                throw new OutOfMemoryException("Injected pool allocation failure.");
#pragma warning restore CA2201
            }
            var buffer = new T[minimumLength];
            Assert.True(Active.Add(buffer));
            return buffer;
        }

        public override void Return(T[] array, bool clearArray = false)
        {
            Assert.True(Active.Remove(array), "Buffer returned more than once.");
            ReturnCount++;
            // Poison returned buffers deterministically, without racing another pool user.
            Array.Clear(array, 0, array.Length);
        }
    }

    private sealed class ThrowingOutputStream : MemoryStream
    {
        public override void Write(byte[] buffer, int offset, int count) => throw new IOException();

        public override void WriteByte(byte value) => throw new IOException();

        public override Task WriteAsync(
            byte[] buffer,
            int offset,
            int count,
            CancellationToken cancellationToken
        ) => Task.FromException(new IOException());

#if !LEGACY_DOTNET
        public override void Write(ReadOnlySpan<byte> buffer) => throw new IOException();

        public override ValueTask WriteAsync(
            ReadOnlyMemory<byte> buffer,
            CancellationToken cancellationToken = default
        ) => new(Task.FromException(new IOException()));
#endif
    }
}
