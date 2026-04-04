using System;
using System.Buffers;
using System.IO;
using System.Linq;
using SharpCompress.IO;
using Xunit;

namespace SharpCompress.Test.Streams;

public class PooledMemoryStreamTests
{
    [Fact]
    public void GrowsUsingFixedSizeBlocks()
    {
        var pool = new TrackingArrayPool();

        using var stream = new PooledMemoryStream(capacity: 0, blockSize: 8, arrayPool: pool);
        stream.Write(new byte[20], 0, 20);

        Assert.Equal(3, pool.RentRequests.Count);
        Assert.All(pool.RentRequests, requested => Assert.Equal(8, requested));
    }

    [Fact]
    public void DisposeReturnsRentedBlocksToPool()
    {
        var pool = new TrackingArrayPool();
        var stream = new PooledMemoryStream(capacity: 0, blockSize: 8, arrayPool: pool);

        stream.Write(new byte[17], 0, 17);
        stream.Dispose();

        Assert.Equal(pool.RentRequests.Count, pool.ReturnedLengths.Count);
        Assert.All(pool.ReturnedLengths, length => Assert.Equal(8, length));
    }

    [Fact]
    public void GetBufferPromotesToContiguousAndLaterGrowthUsesBlocks()
    {
        var pool = new TrackingArrayPool();

        using var stream = new PooledMemoryStream(capacity: 0, blockSize: 8, arrayPool: pool);
        stream.Write(new byte[10], 0, 10);

        var contiguous = stream.GetBuffer();
        Assert.Equal(16, contiguous.Length);
        Assert.Contains(16, pool.RentRequests);

        stream.Position = stream.Length;
        stream.Write(new byte[10], 0, 10); // grow to 20, forcing demotion + block growth

        var totalBlockRents = pool.RentRequests.Count(size => size == 8);
        Assert.True(totalBlockRents >= 3);
    }

    [Fact]
    public void BufferConstructorCopiesDataAndIsNonExpandable()
    {
        var backing = Enumerable.Range(0, 10).Select(i => (byte)i).ToArray();
        using var stream = new PooledMemoryStream(
            backing,
            2,
            4,
            writable: true,
            publiclyVisible: true
        );

        Assert.Equal(4, stream.Length);
        Assert.Equal(4, stream.Capacity);

        backing[2] = 255;
        Assert.Equal(new byte[] { 2, 3, 4, 5 }, stream.ToArray());

        Assert.Throws<NotSupportedException>(() => stream.Capacity = 5);
    }

    [Fact]
    public void BufferConstructorTryGetBufferRespectsVisibilityFlag()
    {
        var backing = new byte[10];
        using var hidden = new PooledMemoryStream(
            backing,
            1,
            5,
            writable: true,
            publiclyVisible: false
        );
        Assert.False(hidden.TryGetBuffer(out _));

        using var visible = new PooledMemoryStream(
            backing,
            1,
            5,
            writable: true,
            publiclyVisible: true
        );
        Assert.True(visible.TryGetBuffer(out var segment));
        Assert.Equal(0, segment.Offset);
        Assert.Equal(5, segment.Count);
    }

    [Fact]
    public void SetLengthExtendingClearsGap()
    {
        using var stream = new PooledMemoryStream(capacity: 0, blockSize: 8);
        stream.Position = 5;
        stream.WriteByte(42);
        stream.Position = 0;

        var data = stream.ToArray();
        Assert.Equal(6, data.Length);
        Assert.Equal(0, data[0]);
        Assert.Equal(0, data[4]);
        Assert.Equal(42, data[5]);
    }

    [Fact]
    public void MethodsThrowAfterDispose()
    {
        using var stream = new PooledMemoryStream(capacity: 0, blockSize: 8);
        stream.WriteByte(1);
        stream.Dispose();

        Assert.Throws<ObjectDisposedException>(() => stream.ReadByte());
        Assert.Throws<ObjectDisposedException>(() => stream.ToArray());
        Assert.Throws<ObjectDisposedException>(() => stream.GetBuffer());
    }

    private sealed class TrackingArrayPool : ArrayPool<byte>
    {
        public readonly System.Collections.Generic.List<int> RentRequests = new();
        public readonly System.Collections.Generic.List<int> ReturnedLengths = new();

        public override byte[] Rent(int minimumLength)
        {
            RentRequests.Add(minimumLength);
            return new byte[minimumLength];
        }

        public override void Return(byte[] array, bool clearArray = false)
        {
            ReturnedLengths.Add(array.Length);
            if (clearArray)
            {
                Array.Clear(array, 0, array.Length);
            }
        }
    }
}
