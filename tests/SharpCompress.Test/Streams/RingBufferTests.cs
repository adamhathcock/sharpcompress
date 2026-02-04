using System;
using SharpCompress.IO;
using Xunit;

namespace SharpCompress.Test.Streams;

public class RingBufferTests
{
    #region Constructor Tests

    [Fact]
    public void Constructor_ValidCapacity_SuccessfullyCreates()
    {
        var buffer = new RingBuffer(100);
        Assert.Equal(100, buffer.Capacity);
        Assert.Equal(0, buffer.Length);
        buffer.Dispose();
    }

    [Fact]
    public void Constructor_ZeroCapacity_ThrowsArgumentOutOfRangeException()
    {
        var ex = Assert.Throws<ArgumentOutOfRangeException>(() => new RingBuffer(0));
        Assert.Contains("Capacity must be positive", ex.Message);
    }

    [Fact]
    public void Constructor_NegativeCapacity_ThrowsArgumentOutOfRangeException()
    {
        var ex = Assert.Throws<ArgumentOutOfRangeException>(() => new RingBuffer(-10));
        Assert.Contains("Capacity must be positive", ex.Message);
    }

    #endregion

    #region Write Tests

    [Fact]
    public void Write_DataWithinCapacity_UpdatesLengthCorrectly()
    {
        var buffer = new RingBuffer(100);
        var data = new byte[] { 1, 2, 3, 4, 5 };

        buffer.Write(data, 0, 5);
        Assert.Equal(5, buffer.Length);

        buffer.Dispose();
    }

    [Fact]
    public void Write_ZeroBytes_ReturnsWithoutChangingBuffer()
    {
        var buffer = new RingBuffer(100);
        var data = new byte[] { 1, 2, 3, 4, 5 };

        buffer.Write(data, 0, 0);
        Assert.Equal(0, buffer.Length);

        buffer.Dispose();
    }

    [Fact]
    public void Write_DataExceedsCapacity_KeepsLastNBytes()
    {
        var buffer = new RingBuffer(5);
        var data = new byte[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10 };

        buffer.Write(data, 0, 10);
        Assert.Equal(5, buffer.Length);

        var readBuffer = new byte[5];
        buffer.ReadFromEnd(5, readBuffer, 0, 5);
        Assert.Equal(new byte[] { 6, 7, 8, 9, 10 }, readBuffer);

        buffer.Dispose();
    }

    [Fact]
    public void Write_DataEqualToCapacity_FillsCompleteBuffer()
    {
        var buffer = new RingBuffer(5);
        var data = new byte[] { 1, 2, 3, 4, 5 };

        buffer.Write(data, 0, 5);
        Assert.Equal(5, buffer.Length);

        var readBuffer = new byte[5];
        buffer.ReadFromEnd(5, readBuffer, 0, 5);
        Assert.Equal(new byte[] { 1, 2, 3, 4, 5 }, readBuffer);

        buffer.Dispose();
    }

    [Fact]
    public void Write_MultipleWrites_TracksCumulativeLength()
    {
        var buffer = new RingBuffer(100);
        var data1 = new byte[] { 1, 2, 3 };
        var data2 = new byte[] { 4, 5, 6 };

        buffer.Write(data1, 0, 3);
        Assert.Equal(3, buffer.Length);

        buffer.Write(data2, 0, 3);
        Assert.Equal(6, buffer.Length);

        buffer.Dispose();
    }

    [Fact]
    public void Write_WrapAround_CircularWritePosition()
    {
        var buffer = new RingBuffer(5);
        var data1 = new byte[] { 1, 2, 3, 4, 5 };
        var data2 = new byte[] { 6, 7 };

        buffer.Write(data1, 0, 5);
        buffer.Write(data2, 0, 2);
        Assert.Equal(5, buffer.Length);

        var readBuffer = new byte[5];
        buffer.ReadFromEnd(5, readBuffer, 0, 5);
        // After writing 5 bytes (full), writePos=0. After writing 2 more bytes,
        // writePos=2, and buffer contains [6, 7, 3, 4, 5] (last 5 bytes total)
        Assert.Equal(new byte[] { 3, 4, 5, 6, 7 }, readBuffer);

        buffer.Dispose();
    }

    [Fact]
    public void Write_LargeDataWhenBufferFull_ReplacesOldData()
    {
        var buffer = new RingBuffer(10);
        var data1 = new byte[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10 };
        var data2 = new byte[] { 11, 12, 13, 14, 15, 16, 17, 18, 19, 20 };

        buffer.Write(data1, 0, 10);
        buffer.Write(data2, 0, 10);

        var readBuffer = new byte[10];
        buffer.ReadFromEnd(10, readBuffer, 0, 10);
        Assert.Equal(data2, readBuffer);

        buffer.Dispose();
    }

    #endregion

    #region ReadFromEnd Tests

    [Fact]
    public void ReadFromEnd_ValidPosition_ReturnsCorrectData()
    {
        var buffer = new RingBuffer(100);
        var data = new byte[] { 1, 2, 3, 4, 5 };
        buffer.Write(data, 0, 5);

        var readBuffer = new byte[5];
        int read = buffer.ReadFromEnd(5, readBuffer, 0, 5);

        Assert.Equal(5, read);
        Assert.Equal(new byte[] { 1, 2, 3, 4, 5 }, readBuffer);

        buffer.Dispose();
    }

    [Fact]
    public void ReadFromEnd_WithWrapAround_ReturnsCorrectData()
    {
        var buffer = new RingBuffer(5);
        var data1 = new byte[] { 1, 2, 3, 4, 5 };
        var data2 = new byte[] { 6, 7 };

        buffer.Write(data1, 0, 5);
        buffer.Write(data2, 0, 2);

        var readBuffer = new byte[5];
        buffer.ReadFromEnd(5, readBuffer, 0, 5);
        // After writing 5 bytes, writePos=0. After writing 2 more bytes,
        // writePos=2, buffer=[6, 7, 3, 4, 5]
        Assert.Equal(new byte[] { 3, 4, 5, 6, 7 }, readBuffer);

        buffer.Dispose();
    }

    [Fact]
    public void ReadFromEnd_PartialRead_ReturnsAvailableBytes()
    {
        var buffer = new RingBuffer(100);
        var data = new byte[] { 1, 2, 3, 4, 5 };
        buffer.Write(data, 0, 5);

        var readBuffer = new byte[10];
        int read = buffer.ReadFromEnd(3, readBuffer, 0, 10);

        Assert.Equal(3, read);
        Assert.Equal(new byte[] { 3, 4, 5, 0, 0, 0, 0, 0, 0, 0 }, readBuffer);

        buffer.Dispose();
    }

    [Fact]
    public void ReadFromEnd_FullCapacity_ReadsAllData()
    {
        var buffer = new RingBuffer(10);
        var data = new byte[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10 };
        buffer.Write(data, 0, 10);

        var readBuffer = new byte[10];
        buffer.ReadFromEnd(10, readBuffer, 0, 10);

        Assert.Equal(data, readBuffer);

        buffer.Dispose();
    }

    [Fact]
    public void ReadFromEnd_ZeroBytesFromEnd_ReturnsZero()
    {
        var buffer = new RingBuffer(100);
        var data = new byte[] { 1, 2, 3, 4, 5 };
        buffer.Write(data, 0, 5);

        var readBuffer = new byte[5];
        int read = buffer.ReadFromEnd(0, readBuffer, 0, 5);

        Assert.Equal(0, read);

        buffer.Dispose();
    }

    [Fact]
    public void ReadFromEnd_ExceedsBufferLength_ThrowsArgumentOutOfRangeException()
    {
        var buffer = new RingBuffer(100);
        var data = new byte[] { 1, 2, 3, 4, 5 };
        buffer.Write(data, 0, 5);

        var readBuffer = new byte[10];
        var ex = Assert.Throws<ArgumentOutOfRangeException>(() =>
            buffer.ReadFromEnd(6, readBuffer, 0, 10)
        );
        Assert.Contains("outside buffer range", ex.Message);

        buffer.Dispose();
    }

    [Fact]
    public void ReadFromEnd_NegativeBytesFromEnd_ReturnsZero()
    {
        var buffer = new RingBuffer(100);
        var data = new byte[] { 1, 2, 3, 4, 5 };
        buffer.Write(data, 0, 5);

        var readBuffer = new byte[5];
        int read = buffer.ReadFromEnd(-1, readBuffer, 0, 5);

        Assert.Equal(0, read);

        buffer.Dispose();
    }

    [Fact]
    public void ReadFromEnd_CountZero_ReturnsZero()
    {
        var buffer = new RingBuffer(100);
        var data = new byte[] { 1, 2, 3, 4, 5 };
        buffer.Write(data, 0, 5);

        var readBuffer = new byte[5];
        int read = buffer.ReadFromEnd(5, readBuffer, 0, 0);

        Assert.Equal(0, read);

        buffer.Dispose();
    }

    #endregion

    #region CanReadFromEnd Tests

    [Fact]
    public void CanReadFromEnd_ValidPosition_ReturnsTrue()
    {
        var buffer = new RingBuffer(100);
        var data = new byte[] { 1, 2, 3, 4, 5 };
        buffer.Write(data, 0, 5);

        Assert.True(buffer.CanReadFromEnd(1));
        Assert.True(buffer.CanReadFromEnd(5));

        buffer.Dispose();
    }

    [Fact]
    public void CanReadFromEnd_PositionExceedsLength_ReturnsFalse()
    {
        var buffer = new RingBuffer(100);
        var data = new byte[] { 1, 2, 3, 4, 5 };
        buffer.Write(data, 0, 5);

        Assert.False(buffer.CanReadFromEnd(6));
        Assert.False(buffer.CanReadFromEnd(100));

        buffer.Dispose();
    }

    [Fact]
    public void CanReadFromEnd_NegativePosition_ReturnsFalse()
    {
        var buffer = new RingBuffer(100);
        var data = new byte[] { 1, 2, 3, 4, 5 };
        buffer.Write(data, 0, 5);

        Assert.False(buffer.CanReadFromEnd(-1));

        buffer.Dispose();
    }

    [Fact]
    public void CanReadFromEnd_Zero_ReturnsTrue()
    {
        var buffer = new RingBuffer(100);
        var data = new byte[] { 1, 2, 3, 4, 5 };
        buffer.Write(data, 0, 5);

        Assert.True(buffer.CanReadFromEnd(0));

        buffer.Dispose();
    }

    #endregion

    #region Properties Tests

    [Fact]
    public void Capacity_ReturnsSetCapacity()
    {
        var buffer = new RingBuffer(42);
        Assert.Equal(42, buffer.Capacity);
        buffer.Dispose();
    }

    [Fact]
    public void Length_InitiallyZero()
    {
        var buffer = new RingBuffer(100);
        Assert.Equal(0, buffer.Length);
        buffer.Dispose();
    }

    [Fact]
    public void Length_UpdatesAfterWrite()
    {
        var buffer = new RingBuffer(100);
        var data = new byte[] { 1, 2, 3, 4, 5 };

        Assert.Equal(0, buffer.Length);
        buffer.Write(data, 0, 5);
        Assert.Equal(5, buffer.Length);

        buffer.Dispose();
    }

    [Fact]
    public void Length_CapsAtCapacity()
    {
        var buffer = new RingBuffer(5);
        var data = new byte[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10 };

        buffer.Write(data, 0, 10);
        Assert.Equal(5, buffer.Length);

        buffer.Dispose();
    }

    #endregion

    #region Dispose Tests

    [Fact]
    public void Dispose_ReturnsArrayToPool()
    {
        var buffer = new RingBuffer(100);
        buffer.Dispose();
    }

    [Fact]
    public void Dispose_WriteAfterDispose_ThrowsObjectDisposedException()
    {
        var buffer = new RingBuffer(100);
        buffer.Dispose();

        var data = new byte[] { 1, 2, 3 };
        Assert.Throws<ObjectDisposedException>(() => buffer.Write(data, 0, 3));
    }

    [Fact]
    public void Dispose_ReadFromEndAfterDispose_ThrowsObjectDisposedException()
    {
        var buffer = new RingBuffer(100);
        var data = new byte[] { 1, 2, 3, 4, 5 };
        buffer.Write(data, 0, 5);
        buffer.Dispose();

        var readBuffer = new byte[5];
        Assert.Throws<ObjectDisposedException>(() => buffer.ReadFromEnd(5, readBuffer, 0, 5));
    }

    [Fact]
    public void Dispose_IdempotentDispose_NoException()
    {
        var buffer = new RingBuffer(100);
        buffer.Dispose();
        buffer.Dispose();
    }

    #endregion

    #region Integration Tests

    [Fact]
    public void Integration_SequentialReadsWithMultipleWrites()
    {
        var buffer = new RingBuffer(10);

        buffer.Write(new byte[] { 1, 2, 3 }, 0, 3);
        var readBuffer1 = new byte[3];
        buffer.ReadFromEnd(3, readBuffer1, 0, 3);
        Assert.Equal(new byte[] { 1, 2, 3 }, readBuffer1);

        buffer.Write(new byte[] { 4, 5, 6 }, 0, 3);
        var readBuffer2 = new byte[6];
        buffer.ReadFromEnd(6, readBuffer2, 0, 6);
        Assert.Equal(new byte[] { 1, 2, 3, 4, 5, 6 }, readBuffer2);

        buffer.Dispose();
    }

    [Fact]
    public void Integration_ComplexWrapAroundScenario()
    {
        var buffer = new RingBuffer(5);

        buffer.Write(new byte[] { 1, 2, 3, 4, 5 }, 0, 5);
        // Buffer: [1, 2, 3, 4, 5], writePos=0
        buffer.Write(new byte[] { 6, 7, 8 }, 0, 3);
        // After writing 3 more bytes: Buffer = [6, 7, 8, 4, 5], writePos=3
        Assert.Equal(5, buffer.Length);

        var readBuffer = new byte[5];
        buffer.ReadFromEnd(5, readBuffer, 0, 5);
        Assert.Equal(new byte[] { 4, 5, 6, 7, 8 }, readBuffer);

        buffer.Write(new byte[] { 9 }, 0, 1);
        // After writing 1 more byte: Buffer = [6, 7, 8, 9, 5], writePos=4
        buffer.ReadFromEnd(5, readBuffer, 0, 5);
        Assert.Equal(new byte[] { 5, 6, 7, 8, 9 }, readBuffer);

        buffer.Dispose();
    }

    [Fact]
    public void Integration_PartialReadsMultipleTimes()
    {
        var buffer = new RingBuffer(100);
        var data = new byte[20];
        for (byte i = 0; i < 20; i++)
        {
            data[i] = i;
        }
        buffer.Write(data, 0, 20);

        var readBuffer1 = new byte[5];
        buffer.ReadFromEnd(10, readBuffer1, 0, 5);
        Assert.Equal(new byte[] { 10, 11, 12, 13, 14 }, readBuffer1);

        var readBuffer2 = new byte[10];
        buffer.ReadFromEnd(20, readBuffer2, 0, 10);
        Assert.Equal(new byte[] { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9 }, readBuffer2);

        buffer.Dispose();
    }

    #endregion
}
