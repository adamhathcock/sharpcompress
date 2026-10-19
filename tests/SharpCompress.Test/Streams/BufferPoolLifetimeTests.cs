using System;
using System.Buffers;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using SharpCompress.Common;
using SharpCompress.Common.Tar.Headers;
using SharpCompress.Common.Zip.Headers;
using SharpCompress.Compressors.LZMA;
using SharpCompress.Compressors.LZMA.LZ;
using SharpCompress.Compressors.LZMA.Utilities;
using SharpCompress.Compressors.Shrink;
using SharpCompress.IO;
using Xunit;

namespace SharpCompress.Test.Streams;

public class BufferPoolLifetimeTests
{
    [Theory]
    [InlineData(false, 0, false)]
    [InlineData(true, 0, false)]
    [InlineData(false, 10000, false)]
    [InlineData(true, 10000, false)]
    [InlineData(false, 1, true)]
    [InlineData(true, 1, true)]
    public async Task Zip64_ExtensibleData_ReturnsBuffer(
        bool useAsync,
        int payloadSize,
        bool truncated
    )
    {
        var pool = new TrackingPool<byte> { ExtraCapacity = 32 };
        using var input = new MemoryStream();
        using (var writer = new BinaryWriter(input, System.Text.Encoding.UTF8, true))
        {
            writer.Write((ulong)(44 + payloadSize + (truncated ? 10000 : 0)));
            writer.Write(new byte[44 + payloadSize]);
            if (!truncated)
            {
                writer.Write((byte)99);
            }
        }
        input.Position = 0;
        var header = new Zip64DirectoryEndHeader(pool);
        if (useAsync)
        {
            using var reader = new AsyncBinaryReader(input, leaveOpen: true);
            if (truncated)
            {
                await Assert.ThrowsAnyAsync<SharpCompressException>(async () =>
                    await header.Read(reader)
                );
            }
            else
            {
                await header.Read(reader);
            }
        }
        else
        {
            using var reader = new BinaryReader(input, System.Text.Encoding.UTF8, true);
            if (truncated)
            {
                Assert.ThrowsAny<SharpCompressException>(() => header.Read(reader));
            }
            else
            {
                header.Read(reader);
            }
        }
        if (!truncated)
        {
            Assert.Equal(99, input.ReadByte());
        }
        Assert.Equal(payloadSize == 0 ? 0 : 1, pool.RentCount);
        Assert.Equal(pool.RentCount, pool.ReturnCount);
        Assert.Empty(pool.Active);
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task Shrink_Dispose_ReturnsBuffers(bool useAsync, bool asyncDispose)
    {
        var pool = new TrackingPool<byte> { ExtraCapacity = 32 };
        using var input = new MemoryStream(new byte[] { 65, 132, 0 });
        using var stream = new ShrinkStream(input, 2, pool);
        var buffer = new byte[1];
        foreach (var expected in new byte[] { 65, 66 })
        {
            var read = useAsync
                ? await stream.ReadAsync(buffer, 0, 1, CancellationToken.None)
                : stream.Read(buffer, 0, 1);
            Assert.Equal(1, read);
            Assert.Equal(expected, buffer[0]);
        }
        Assert.Equal(0, stream.Read(buffer, 0, 1));
        Assert.Equal(2, pool.RentCount);
        Assert.Equal(1, pool.ReturnCount);
        Assert.Single(pool.Active);

#if !LEGACY_DOTNET
        if (asyncDispose)
        {
            await stream.DisposeAsync();
        }
        else
#endif
        {
            stream.Dispose();
        }
        stream.Dispose();
        Assert.Equal(2, pool.ReturnCount);
        Assert.Empty(pool.Active);
        Assert.True(input.CanRead);
        Assert.False(stream.CanRead);
        Assert.Throws<ObjectDisposedException>(() => stream.Read(buffer, 0, 1));
        await Assert.ThrowsAsync<ObjectDisposedException>(() =>
            stream.ReadAsync(buffer, 0, 1, CancellationToken.None)
        );
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Shrink_InvalidData_ReturnsBuffers(bool useAsync)
    {
        var pool = new TrackingPool<byte> { ExtraCapacity = 32 };
        using var input = new MemoryStream(new byte[] { 65, 132, 0 });
        using var stream = new ShrinkStream(input, int.MaxValue, pool);
        var buffer = new byte[1];
        if (useAsync)
        {
            await Assert.ThrowsAsync<InvalidFormatException>(() =>
                stream.ReadAsync(buffer, 0, 1, CancellationToken.None)
            );
        }
        else
        {
            Assert.Throws<InvalidFormatException>(() => stream.Read(buffer, 0, 1));
        }
        Assert.Equal(2, pool.RentCount);
        Assert.Equal(2, pool.ReturnCount);
        Assert.Empty(pool.Active);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Shrink_Growth_ReturnsBuffers(bool failGrowth)
    {
        // Literal-only nine-bit codes fill the first output rental without increasing code size.
        const int count = 70000;
        var input = new byte[(count * 9 + 7) / 8];
        for (var i = 0; i < count; i++)
        {
            var bit = i * 9;
            input[bit / 8] |= (byte)(65 << (bit % 8));
            input[bit / 8 + 1] |= (byte)(65 >> (8 - bit % 8));
        }
        var pool = new TrackingPool<byte> { FailRentNumber = failGrowth ? 2 : 0 };
        if (failGrowth)
        {
            Assert.Throws<OutOfMemoryException>(() =>
                HwUnshrink.UnshrinkGrowing(input, input.Length, count, pool, out _, out _)
            );
            Assert.Equal(1, pool.ReturnCount);
        }
        else
        {
            var status = HwUnshrink.UnshrinkGrowing(
                input,
                input.Length,
                count,
                pool,
                out var output,
                out var used
            );
            try
            {
                Assert.Equal(HwUnshrink.UnshrnkStatus.Ok, status);
                Assert.Equal(count, used);
                Assert.All(output, value => Assert.Equal(65, value));
                Assert.Equal(1, pool.ReturnCount);
            }
            finally
            {
                pool.Return(output);
            }
            Assert.Equal(2, pool.ReturnCount);
        }
        Assert.Empty(pool.Active);
    }

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
#if STREAM_DISPOSEASYNC
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
        internal int FailRentNumber { get; set; }
        internal int ExtraCapacity { get; set; }

        public override T[] Rent(int minimumLength)
        {
            RentCount++;
            if (FailNextRent || RentCount == FailRentNumber)
            {
                FailNextRent = false;
#pragma warning disable CA2201 // Simulate allocation failure without exhausting process memory.
                throw new OutOfMemoryException("Injected pool allocation failure.");
#pragma warning restore CA2201
            }
            var buffer = new T[minimumLength + ExtraCapacity];
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
