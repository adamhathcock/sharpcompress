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
    public void TryGetBufferReturnsSegmentWhenOpen()
    {
        using var stream = new PooledMemoryStream(capacity: 0, blockSize: 8);
        stream.Write(new byte[] { 1, 2, 3, 4 }, 0, 4);

        Assert.True(stream.TryGetBuffer(out var segment));
        Assert.Equal(0, segment.Offset);
        Assert.Equal(4, segment.Count);
        Assert.Equal(1, segment.Array![0]);
    }

    [Fact]
    public void CapacitySetterCanGrowAndShrinkWithinLength()
    {
        using var stream = new PooledMemoryStream(capacity: 16, blockSize: 8);
        stream.Write(new byte[6], 0, 6);

        stream.Capacity = 24;
        Assert.Equal(24, stream.Capacity);

        stream.Capacity = 8;
        Assert.Equal(8, stream.Capacity);
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

    [Fact]
    public void MultipleGetBufferCallsReturnSameArray()
    {
        using var stream = new PooledMemoryStream(capacity: 0, blockSize: 8);
        stream.Write(new byte[] { 1, 2, 3 }, 0, 3);

        var buffer1 = stream.GetBuffer();
        var buffer2 = stream.GetBuffer();

        Assert.Same(buffer1, buffer2);
        Assert.Equal(1, buffer1[0]);
        Assert.Equal(2, buffer1[1]);
        Assert.Equal(3, buffer1[2]);
    }

    [Fact]
    public void SeekBeyondMaxLengthThrows()
    {
        using var stream = new PooledMemoryStream();
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            stream.Seek(int.MaxValue + 1L, SeekOrigin.Begin)
        );
    }

    private sealed class TrackingArrayPool : ArrayPool<byte>
    {
        private const byte RentedBufferFillValue = 0x5A;

        public readonly System.Collections.Generic.List<int> RentRequests = new();
        public readonly System.Collections.Generic.List<int> ReturnedLengths = new();

        public override byte[] Rent(int minimumLength)
        {
            RentRequests.Add(minimumLength);

            var array = new byte[minimumLength];
            for (var i = 0; i < array.Length; i++)
            {
                array[i] = RentedBufferFillValue;
            }

            return array;
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
