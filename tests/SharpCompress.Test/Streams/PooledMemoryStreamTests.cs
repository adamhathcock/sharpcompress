using System;
using System.Buffers;
using System.IO;
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
    public void OverRentedBlocksUseLogicalBlockSize()
    {
        var pool = new FilledOverRentingArrayPool(extraLength: 8, fillValue: 0x5A);

        using var stream = new PooledMemoryStream(capacity: 0, blockSize: 8, arrayPool: pool);
        stream.Write(new byte[] { 1, 2, 3, 4, 5 }, 0, 5);

        stream.Position = 10;
        stream.Write(new byte[] { 42, 43, 44, 45, 46, 47, 48, 49, 50, 51 }, 0, 10);

        Assert.Equal(3, pool.RentRequests.Count);
        Assert.All(pool.RentRequests, requested => Assert.Equal(8, requested));
        Assert.All(pool.RentedLengths, length => Assert.Equal(16, length));

        var expected = new byte[]
        {
            1,
            2,
            3,
            4,
            5,
            0,
            0,
            0,
            0,
            0,
            42,
            43,
            44,
            45,
            46,
            47,
            48,
            49,
            50,
            51,
        };

        Assert.Equal(expected, stream.ToArray());

        stream.Position = 0;
        var roundTrip = new byte[expected.Length];
        Assert.Equal(expected.Length, stream.Read(roundTrip, 0, roundTrip.Length));
        Assert.Equal(expected, roundTrip);
    }

    [Fact]
    public void GetBufferReturnsArraySizedToCapacityWithoutTouchingPool()
    {
        var pool = new OverRentingArrayPool(extraLength: 8);

        using var stream = new PooledMemoryStream(capacity: 0, blockSize: 8, arrayPool: pool);
        stream.Write(new byte[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10 }, 0, 10);

        var rentsBefore = pool.RentRequests.Count;
        var returnsBefore = pool.ReturnedLengths.Count;

        var buffer = stream.GetBuffer();
        Assert.Equal(16, buffer.Length);
        Assert.Equal(1, buffer[0]);
        Assert.Equal(10, buffer[9]);
        Assert.Equal(0, buffer[10]);
        Assert.Equal(0, buffer[15]);
        Assert.Equal(rentsBefore, pool.RentRequests.Count);
        Assert.Equal(returnsBefore, pool.ReturnedLengths.Count);

        buffer[0] = 255;
        stream.Position = 0;
        Assert.Equal(1, stream.ReadByte());
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
    public void TryGetBufferReturnsArraySizedToCapacityWithoutTouchingPool()
    {
        var pool = new OverRentingArrayPool(extraLength: 8);

        using var stream = new PooledMemoryStream(capacity: 0, blockSize: 8, arrayPool: pool);
        stream.Write(new byte[] { 1, 2, 3, 4 }, 0, 4);

        var rentsBefore = pool.RentRequests.Count;
        var returnsBefore = pool.ReturnedLengths.Count;

        Assert.True(stream.TryGetBuffer(out var segment));
        Assert.Equal(0, segment.Offset);
        Assert.Equal(4, segment.Count);
        Assert.Equal(8, segment.Array!.Length);
        Assert.Equal(1, segment.Array[0]);
        Assert.Equal(4, segment.Array[3]);
        Assert.Equal(0, segment.Array[4]);
        Assert.Equal(0, segment.Array[7]);
        Assert.Equal(rentsBefore, pool.RentRequests.Count);
        Assert.Equal(returnsBefore, pool.ReturnedLengths.Count);

        segment.Array[0] = 255;
        stream.Position = 0;
        Assert.Equal(1, stream.ReadByte());
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
    public void MultipleGetBufferCallsReturnDifferentArrays()
    {
        using var stream = new PooledMemoryStream(capacity: 0, blockSize: 8);
        stream.Write(new byte[] { 1, 2, 3 }, 0, 3);

        var buffer1 = stream.GetBuffer();
        var buffer2 = stream.GetBuffer();

        Assert.NotSame(buffer1, buffer2);
        Assert.Equal(buffer1, buffer2);
    }

    [Fact]
    public void SeekBeyondMaxLengthThrows()
    {
        using var stream = new PooledMemoryStream();
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            stream.Seek(int.MaxValue + 1L, SeekOrigin.Begin)
        );
    }

    [Fact]
    public void DisposeAfterGetBufferDoesNotReturnExposedArrayToPool()
    {
        var pool = new OverRentingArrayPool(extraLength: 8);
        byte[] buffer;

        using (var stream = new PooledMemoryStream(capacity: 0, blockSize: 8, pool))
        {
            stream.Write(new byte[] { 1, 2, 3 }, 0, 3);
            buffer = stream.GetBuffer();

            Assert.NotNull(buffer);
            Assert.NotEmpty(pool.RentRequests);
        }

        Assert.DoesNotContain(buffer, pool.ReturnedArrays);
        Assert.Equal(1, buffer[0]);
        Assert.Equal(2, buffer[1]);
        Assert.Equal(3, buffer[2]);
    }

    [Fact]
    public void DisposeAfterTryGetBufferDoesNotReturnExposedArrayToPool()
    {
        var pool = new OverRentingArrayPool(extraLength: 8);
        ArraySegment<byte> segment;

        using (var stream = new PooledMemoryStream(capacity: 0, blockSize: 8, pool))
        {
            stream.Write(new byte[] { 1, 2, 3 }, 0, 3);

            Assert.True(stream.TryGetBuffer(out segment));
            Assert.NotNull(segment.Array);
            Assert.NotEmpty(pool.RentRequests);
        }

        Assert.DoesNotContain(segment.Array!, pool.ReturnedArrays);
        Assert.Equal(1, segment.Array![segment.Offset]);
        Assert.Equal(2, segment.Array[segment.Offset + 1]);
        Assert.Equal(3, segment.Array[segment.Offset + 2]);
    }

    [Fact]
    public void SetLengthNearIntMaxValueThrowsIOExceptionWhenBlockRoundingOverflows()
    {
        using var stream = new PooledMemoryStream(capacity: 0, blockSize: 8);
        var length = int.MaxValue - 1L;

        Assert.Throws<IOException>(() => stream.SetLength(length));
        Assert.Equal(0, stream.Length);
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

    private sealed class OverRentingArrayPool : ArrayPool<byte>
    {
        private readonly int _extraLength;

        public OverRentingArrayPool(int extraLength)
        {
            _extraLength = extraLength;
        }

        public readonly System.Collections.Generic.List<int> RentRequests = new();
        public readonly System.Collections.Generic.List<int> ReturnedLengths = new();
        public readonly System.Collections.Generic.List<byte[]> ReturnedArrays = new();

        public override byte[] Rent(int minimumLength)
        {
            RentRequests.Add(minimumLength);
            return new byte[minimumLength + _extraLength];
        }

        public override void Return(byte[] array, bool clearArray = false)
        {
            ReturnedLengths.Add(array.Length);
            ReturnedArrays.Add(array);
            if (clearArray)
            {
                Array.Clear(array, 0, array.Length);
            }
        }
    }

    private sealed class FilledOverRentingArrayPool : ArrayPool<byte>
    {
        private readonly int _extraLength;
        private readonly byte _fillValue;

        public FilledOverRentingArrayPool(int extraLength, byte fillValue)
        {
            _extraLength = extraLength;
            _fillValue = fillValue;
        }

        public readonly System.Collections.Generic.List<int> RentRequests = new();
        public readonly System.Collections.Generic.List<int> RentedLengths = new();

        public override byte[] Rent(int minimumLength)
        {
            RentRequests.Add(minimumLength);

            var array = new byte[minimumLength + _extraLength];
            RentedLengths.Add(array.Length);
            for (var i = 0; i < array.Length; i++)
            {
                array[i] = _fillValue;
            }
            return array;
        }

        public override void Return(byte[] array, bool clearArray = false)
        {
            if (clearArray)
            {
                Array.Clear(array, 0, array.Length);
            }
        }
    }
}
