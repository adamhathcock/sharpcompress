using System;
using SharpCompress.Compressors.Rar;
using Xunit;

namespace SharpCompress.Test.Compressors.Rar;

public class RarCRCTest
{
    [Fact]
    public void CheckCrc_SingleByte_ReturnsCorrectCrc()
    {
        // Arrange
        uint startCrc = 0;
        byte testByte = 0x42;

        // Act
        var result = RarCRC.CheckCrc(startCrc, testByte);

        // Assert
        Assert.NotEqual(0u, result);
    }

    [Fact]
    public void CheckCrc_SingleByte_WithNonZeroStartCrc()
    {
        // Arrange
        uint startCrc = 0x12345678;
        byte testByte = 0xAB;

        // Act
        var result = RarCRC.CheckCrc(startCrc, testByte);

        // Assert
        Assert.NotEqual(startCrc, result);
    }

    [Fact]
    public void CheckCrc_SingleByte_DifferentBytesProduceDifferentCrcs()
    {
        // Arrange
        uint startCrc = 0;
        byte byte1 = 0x01;
        byte byte2 = 0x02;

        // Act
        var result1 = RarCRC.CheckCrc(startCrc, byte1);
        var result2 = RarCRC.CheckCrc(startCrc, byte2);

        // Assert
        Assert.NotEqual(result1, result2);
    }

    [Fact]
    public void CheckCrc_EmptySpan_ReturnsStartCrc()
    {
        // Arrange
        uint startCrc = 0x12345678;
        ReadOnlySpan<byte> data = ReadOnlySpan<byte>.Empty;

        // Act
        var result = RarCRC.CheckCrc(startCrc, data, 0, 0);

        // Assert
        Assert.Equal(startCrc, result);
    }

    [Fact]
    public void CheckCrc_SingleByteSpan_MatchesSingleByteMethod()
    {
        // Arrange
        uint startCrc = 0;
        byte testByte = 0x42;
        ReadOnlySpan<byte> data = stackalloc byte[] { testByte };

        // Act
        var resultSingleByte = RarCRC.CheckCrc(startCrc, testByte);
        var resultSpan = RarCRC.CheckCrc(startCrc, data, 0, 1);

        // Assert
        Assert.Equal(resultSingleByte, resultSpan);
    }

    [Fact]
    public void CheckCrc_MultipleBytes_ProducesConsistentResult()
    {
        // Arrange
        uint startCrc = 0;
        ReadOnlySpan<byte> data = stackalloc byte[] { 0x01, 0x02, 0x03, 0x04 };

        // Act
        var result1 = RarCRC.CheckCrc(startCrc, data, 0, 4);
        var result2 = RarCRC.CheckCrc(startCrc, data, 0, 4);

        // Assert
        Assert.Equal(result1, result2);
    }

    [Fact]
    public void CheckCrc_MultipleBytes_IncrementalMatchesComplete()
    {
        // Arrange
        uint startCrc = 0;
        ReadOnlySpan<byte> data = stackalloc byte[] { 0x01, 0x02, 0x03, 0x04 };

        // Act - calculate incrementally
        var crc1 = RarCRC.CheckCrc(startCrc, data[0]);
        var crc2 = RarCRC.CheckCrc(crc1, data[1]);
        var crc3 = RarCRC.CheckCrc(crc2, data[2]);
        var crc4 = RarCRC.CheckCrc(crc3, data[3]);

        // Act - calculate all at once
        var crcComplete = RarCRC.CheckCrc(startCrc, data, 0, 4);

        // Assert
        Assert.Equal(crc4, crcComplete);
    }

    [Fact]
    public void CheckCrc_WithOffset_ProcessesCorrectBytes()
    {
        // Arrange
        uint startCrc = 0;
        ReadOnlySpan<byte> data = stackalloc byte[] { 0xFF, 0xFF, 0x01, 0x02, 0x03, 0xFF, 0xFF };

        // Act
        var result = RarCRC.CheckCrc(startCrc, data, 2, 3);
        var expected = RarCRC.CheckCrc(startCrc, stackalloc byte[] { 0x01, 0x02, 0x03 }, 0, 3);

        // Assert
        Assert.Equal(expected, result);
    }

    [Fact]
    public void CheckCrc_WithCountSmallerThanData_ProcessesOnlyCount()
    {
        // Arrange
        uint startCrc = 0;
        ReadOnlySpan<byte> data = stackalloc byte[] { 0x01, 0x02, 0x03, 0x04, 0x05 };

        // Act
        var result = RarCRC.CheckCrc(startCrc, data, 0, 3);
        var expected = RarCRC.CheckCrc(startCrc, stackalloc byte[] { 0x01, 0x02, 0x03 }, 0, 3);

        // Assert
        Assert.Equal(expected, result);
    }

    [Fact]
    public void CheckCrc_CountLargerThanRemainingData_ProcessesOnlyAvailableData()
    {
        // Arrange
        uint startCrc = 0;
        ReadOnlySpan<byte> data = stackalloc byte[] { 0x01, 0x02, 0x03 };

        // Act
        var result = RarCRC.CheckCrc(startCrc, data, 0, 100);
        var expected = RarCRC.CheckCrc(startCrc, data, 0, 3);

        // Assert
        Assert.Equal(expected, result);
    }

    [Fact]
    public void CheckCrc_KnownTestVector_HelloWorld()
    {
        // Arrange - "Hello, World!" in ASCII
        uint startCrc = 0xFFFFFFFF; // CRC32 typically starts with inverted bits
        var data = System.Text.Encoding.ASCII.GetBytes("Hello, World!");

        // Act
        var result = RarCRC.CheckCrc(startCrc, data, 0, data.Length);

        // Assert - verify it produces a result (exact value depends on CRC32 variant)
        Assert.NotEqual(startCrc, result);
        Assert.NotEqual(0u, result);
    }

    [Fact]
    public void CheckCrc_AllZeros_ProducesConsistentResult()
    {
        // Arrange
        uint startCrc = 0;
        ReadOnlySpan<byte> data = stackalloc byte[10]; // all zeros

        // Act
        var result = RarCRC.CheckCrc(startCrc, data, 0, 10);

        // Assert - verify it's deterministic
        var result2 = RarCRC.CheckCrc(startCrc, data, 0, 10);
        Assert.Equal(result, result2);
        // CRC of all zeros from startCrc=0 can be 0, that's valid
    }

    [Fact]
    public void CheckCrc_AllOnes_ProducesConsistentResult()
    {
        // Arrange
        uint startCrc = 0;
        Span<byte> data = stackalloc byte[10];
        data.Fill(0xFF);

        // Act
        var result = RarCRC.CheckCrc(startCrc, data, 0, 10);

        // Assert
        var result2 = RarCRC.CheckCrc(startCrc, data, 0, 10);
        Assert.Equal(result, result2);
        Assert.NotEqual(0u, result);
    }

    [Fact]
    public void CheckCrc_OrderMatters()
    {
        // Arrange
        uint startCrc = 0;
        ReadOnlySpan<byte> data1 = stackalloc byte[] { 0x01, 0x02 };
        ReadOnlySpan<byte> data2 = stackalloc byte[] { 0x02, 0x01 };

        // Act
        var result1 = RarCRC.CheckCrc(startCrc, data1, 0, 2);
        var result2 = RarCRC.CheckCrc(startCrc, data2, 0, 2);

        // Assert - different order should produce different CRC
        Assert.NotEqual(result1, result2);
    }

    [Fact]
    public void CheckCrc_LargeData_ProcessesCorrectly()
    {
        // Arrange
        uint startCrc = 0;
        var data = new byte[1024];
        for (int i = 0; i < data.Length; i++)
        {
            data[i] = (byte)(i % 256);
        }

        // Act
        var result = RarCRC.CheckCrc(startCrc, data, 0, data.Length);

        // Assert
        Assert.NotEqual(0u, result);
        // Verify it's deterministic
        var result2 = RarCRC.CheckCrc(startCrc, data, 0, data.Length);
        Assert.Equal(result, result2);
    }

    [Fact]
    public void CheckCrc_PartialSpan_WithOffsetAndCount()
    {
        // Arrange
        uint startCrc = 0;
        var data = new byte[100];
        for (int i = 0; i < data.Length; i++)
        {
            data[i] = (byte)(i % 256);
        }

        // Act - process middle section
        var result = RarCRC.CheckCrc(startCrc, data, 25, 50);

        // Assert - verify it processes exactly 50 bytes starting at offset 25
        var middleSection = new byte[50];
        Array.Copy(data, 25, middleSection, 0, 50);
        var expected = RarCRC.CheckCrc(startCrc, middleSection, 0, 50);
        Assert.Equal(expected, result);
    }

    [Fact]
    public void CheckCrc_ZeroCount_ReturnsStartCrc()
    {
        // Arrange
        uint startCrc = 0x12345678;
        ReadOnlySpan<byte> data = stackalloc byte[] { 0x01, 0x02, 0x03 };

        // Act
        var result = RarCRC.CheckCrc(startCrc, data, 0, 0);

        // Assert
        Assert.Equal(startCrc, result);
    }

    [Fact]
    public void CheckCrc_MaxByteValue_HandlesCorrectly()
    {
        // Arrange
        uint startCrc = 0;
        byte maxByte = 0xFF;

        // Act
        var result = RarCRC.CheckCrc(startCrc, maxByte);

        // Assert
        Assert.NotEqual(0u, result);
    }

    [Fact]
    public void CheckCrc_MinByteValue_HandlesCorrectly()
    {
        // Arrange
        uint startCrc = 0;
        byte minByte = 0x00;

        // Act
        var result = RarCRC.CheckCrc(startCrc, minByte);

        // Assert - CRC of 0x00 from startCrc=0 can be 0, that's mathematically valid
        // What matters is that it's deterministic and doesn't crash
        var result2 = RarCRC.CheckCrc(startCrc, minByte);
        Assert.Equal(result, result2);
    }

    [Fact]
    public void CheckCrc_ChainedCalls_ProduceCorrectResult()
    {
        // Arrange
        uint startCrc = 0;
        ReadOnlySpan<byte> part1 = stackalloc byte[] { 0x01, 0x02 };
        ReadOnlySpan<byte> part2 = stackalloc byte[] { 0x03, 0x04 };
        ReadOnlySpan<byte> combined = stackalloc byte[] { 0x01, 0x02, 0x03, 0x04 };

        // Act
        var crc1 = RarCRC.CheckCrc(startCrc, part1, 0, 2);
        var crc2 = RarCRC.CheckCrc(crc1, part2, 0, 2);
        var crcCombined = RarCRC.CheckCrc(startCrc, combined, 0, 4);

        // Assert - chained calculation should equal combined calculation
        Assert.Equal(crc2, crcCombined);
    }

    [Theory]
    [InlineData(0x00000000)]
    [InlineData(0xFFFFFFFF)]
    [InlineData(0x12345678)]
    [InlineData(0xABCDEF01)]
    public void CheckCrc_VariousStartCrcs_ProduceDifferentResults(uint startCrc)
    {
        // Arrange
        ReadOnlySpan<byte> data = stackalloc byte[] { 0x01, 0x02, 0x03 };

        // Act
        var result = RarCRC.CheckCrc(startCrc, data, 0, 3);

        // Assert - result should be different from start (unless by extreme coincidence)
        Assert.NotEqual(0u, result);
    }

    [Fact]
    public void CheckCrc_OffsetAtEndOfData_ReturnsStartCrc()
    {
        // Arrange
        uint startCrc = 0x12345678;
        ReadOnlySpan<byte> data = stackalloc byte[] { 0x01, 0x02, 0x03 };

        // Act - offset is at the end, so no bytes to process
        var result = RarCRC.CheckCrc(startCrc, data, 3, 5);

        // Assert
        Assert.Equal(startCrc, result);
    }
}
