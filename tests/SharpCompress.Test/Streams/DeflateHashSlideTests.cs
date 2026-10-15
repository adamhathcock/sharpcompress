using System;
using SharpCompress.Compressors.Deflate;
using Xunit;

namespace SharpCompress.Test.Streams;

public class DeflateHashSlideTests
{
    [Theory]
    [InlineData(512)]
    [InlineData(32768)]
    public void All_Unsigned_Links_Match_Scalar_Mapping(int windowSize)
    {
        var table = new short[65536];
        for (var i = 0; i < table.Length; i++)
        {
            table[i] = unchecked((short)i);
        }
        DeflateManager.SlideHashTable(table, table.Length, windowSize);
        for (var i = 0; i < table.Length; i++)
        {
            Assert.Equal(Math.Max(i - windowSize, 0), table[i] & 0xFFFF);
        }
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(7)]
    [InlineData(8)]
    [InlineData(9)]
    [InlineData(15)]
    [InlineData(16)]
    [InlineData(17)]
    public void Tails_And_Unused_Pooled_Elements_Are_Preserved(int count)
    {
        var table = new short[count + 11];
        var expected = new short[table.Length];
        for (var i = 0; i < table.Length; i++)
        {
            var position = (i * 9973) & 0xFFFF;
            table[i] = unchecked((short)position);
            expected[i] = i < count ? (short)Math.Max(position - 32768, 0) : table[i];
        }
        DeflateManager.SlideHashTable(table, count, 32768);
        Assert.Equal(expected, table);
    }
}
