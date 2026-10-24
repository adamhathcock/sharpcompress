using System;
using System.Buffers;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using SharpCompress.Common;
using SharpCompress.Compressors.Xz;
using SharpCompress.Compressors.Xz.Filters;
using Xunit;

namespace SharpCompress.Test.Xz.Filters;

public class BcjBufferLifetimeTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ReturnsPooledBufferExactlyOnceWithoutClosingInput(bool asyncRead)
    {
        var pool = new CountingPool();
        var expected = Enumerable.Range(0, 9000).Select(i => (byte)i).ToArray();
        using var input = new MemoryStream(expected);
        var stream = new BcjFilterStream(input, (_, count, _) => Math.Max(0, count - 4), pool);
        var buffer = new byte[expected.Length + 16];
        var read = asyncRead
            ? await stream.ReadAsync(buffer, 5, expected.Length, CancellationToken.None)
            : stream.Read(buffer, 5, expected.Length);
        Assert.Equal(expected.Length, read);
        Assert.Equal(expected, buffer.Skip(5).Take(read));
        Assert.Equal(1, pool.Rents);
        Assert.Equal(0, pool.Returns);

#if !LEGACY_DOTNET
        await stream.DisposeAsync();
#else
        stream.Dispose();
#endif
        stream.Dispose();
        Assert.Equal(1, pool.Returns);
        Assert.True(input.CanRead);
        Assert.False(stream.CanRead);
        Assert.Throws<ObjectDisposedException>(() => stream.Read(buffer, 0, 1));
        await Assert.ThrowsAsync<ObjectDisposedException>(() =>
            stream.ReadAsync(buffer, 0, 1, CancellationToken.None)
        );
#if !LEGACY_DOTNET
        await Assert.ThrowsAsync<ObjectDisposedException>(async () =>
            await stream.ReadAsync(buffer.AsMemory(), CancellationToken.None)
        );
#endif
        Assert.Equal(1, pool.Returns);
    }

    [Fact]
    public async Task FailedOrCanceledReadStillReturnsLeaseOnDispose()
    {
        var pool = new CountingPool();
        using var input = new ThrowingInput();
        var buffer = new byte[1];
        using (var stream = new BcjFilterStream(input, (_, count, _) => count, pool))
        {
            using var cancellation = new CancellationTokenSource();
            cancellation.Cancel();
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
                stream.ReadAsync(buffer, 0, 1, cancellation.Token)
            );
            await Assert.ThrowsAsync<IOException>(() =>
                stream.ReadAsync(buffer, 0, 1, CancellationToken.None)
            );
            Assert.Equal(0, pool.Returns);
        }
        Assert.Equal(1, pool.Returns);
        Assert.True(input.CanRead);
    }

#if !LEGACY_DOTNET
    [Fact]
    public void RepeatedFilterConstructionReusesTheBuffer()
    {
        var bytes = new byte[16];
        var buffer = new byte[1];
        ReadOne();
        var start = GC.GetAllocatedBytesForCurrentThread();
        for (var i = 0; i < 100; i++)
        {
            ReadOne();
        }
        var allocated = GC.GetAllocatedBytesForCurrentThread() - start;
        Assert.InRange(allocated, 0, 128 * 1024);

        void ReadOne()
        {
            using var input = new MemoryStream(bytes);
            using var filter = new X86Filter();
            filter.SetBaseStream(input);
            Assert.Equal(1, filter.Read(buffer, 0, 1));
        }
    }
#endif

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public async Task XzReleasesFilterChainAtEndOrEarlyDisposal(bool asyncRead, bool readToEnd)
    {
        using var input = new MemoryStream(X86Fixture());
        var xz = new XZStream(input);
        var buffer = new byte[1];
        Assert.Equal(1, await Read(xz, buffer, asyncRead));
        var block = (XZBlock)GetField(xz, "_currentBlock")!;
        var filters = (
            (System.Collections.Generic.Stack<BlockFilter>)GetField(block, "_filters")!
        ).ToArray();
        Assert.Equal(2, filters.Length);
        var owned = filters.Select(filter => filter.BaseStream).ToArray();
        Assert.All(owned, stream => Assert.True(stream.CanRead));

        if (readToEnd)
        {
            while (await Read(xz, buffer, asyncRead) != 0) { }
            Assert.All(owned, stream => Assert.False(stream.CanRead));
        }
#if !LEGACY_DOTNET
        await xz.DisposeAsync();
#else
        xz.Dispose();
#endif
        xz.Dispose();
        Assert.All(owned, stream => Assert.False(stream.CanRead));
        Assert.False(xz.CanRead);
        Assert.True(input.CanRead);
        Assert.Throws<ObjectDisposedException>(() => xz.Read(buffer, 0, 1));
        await Assert.ThrowsAsync<ObjectDisposedException>(() =>
            xz.ReadAsync(buffer, 0, 1, CancellationToken.None)
        );
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task CorruptXzReleasesFilterChainOnDisposal(bool asyncRead)
    {
        var compressed = X86Fixture();
        compressed[compressed.Length - 29] ^= 1;
        using var input = new MemoryStream(compressed);
        using var xz = new XZStream(input);
        var buffer = new byte[1];
        Assert.Equal(1, await Read(xz, buffer, asyncRead));
        var block = (XZBlock)GetField(xz, "_currentBlock")!;
        var filters = (
            (System.Collections.Generic.Stack<BlockFilter>)GetField(block, "_filters")!
        ).ToArray();
        var owned = filters.Select(filter => filter.BaseStream).ToArray();
        await Assert.ThrowsAsync<InvalidFormatException>(async () =>
        {
            while (await Read(xz, buffer, asyncRead) != 0) { }
        });
        xz.Dispose();
        Assert.All(owned, stream => Assert.False(stream.CanRead));
        Assert.True(input.CanRead);
    }

    private static byte[] X86Fixture() =>
        Convert.FromBase64String(BcjStreamingTests.FilterData.First().Data.Item3);

    private static object? GetField(object instance, string name) =>
        instance
            .GetType()
            .GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!
            .GetValue(instance);

    private static async Task<int> Read(Stream stream, byte[] buffer, bool asyncRead) =>
        asyncRead
            ? await stream.ReadAsync(buffer, 0, buffer.Length, CancellationToken.None)
            : stream.Read(buffer, 0, buffer.Length);

    private sealed class CountingPool : ArrayPool<byte>
    {
        private byte[]? rented;
        public int Rents { get; private set; }
        public int Returns { get; private set; }

        public override byte[] Rent(int minimumLength)
        {
            Rents++;
            rented = Enumerable.Repeat((byte)0xCC, minimumLength + 32).ToArray();
            return rented;
        }

        public override void Return(byte[] array, bool clearArray = false)
        {
            Assert.Same(rented, array);
            Returns++;
            for (var i = 0; i < array.Length; i++)
            {
                array[i] = 0xCC;
            }
        }
    }

    private sealed class ThrowingInput : MemoryStream
    {
        public override Task<int> ReadAsync(
            byte[] buffer,
            int offset,
            int count,
            CancellationToken cancellationToken
        ) => Task.FromException<int>(new IOException("Injected read failure"));
    }
}
