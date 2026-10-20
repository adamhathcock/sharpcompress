using System;
using SharpCompress.Compressors.Deflate;
using Xunit;

namespace SharpCompress.Test.Streams;

public class DeflateMatchLengthTests
{
    [Fact]
    public void Every_Mismatch_And_Tail_Matches_Bytewise_Comparison()
    {
        var left = new byte[269];
        new Random(1234).NextBytes(left);
        foreach (var offset in new[] { 0, 1, 3, 7 })
        {
            for (var length = 0; length <= 258; length++)
            {
                var right = (byte[])left.Clone();
                Assert.Equal(
                    length,
                    DeflateManager.CountMatchBytes(
                        left.AsSpan(offset, length),
                        right.AsSpan(offset, length)
                    )
                );
                for (var mismatch = 0; mismatch < length; mismatch++)
                {
                    right[offset + mismatch] ^= 0x80;
                    Assert.Equal(
                        mismatch,
                        DeflateManager.CountMatchBytes(
                            left.AsSpan(offset, length),
                            right.AsSpan(offset, length)
                        )
                    );
                    right[offset + mismatch] ^= 0x80;
                }
            }
        }
    }

    [Fact]
    public void Overlapping_And_Unequal_Slices_Stop_At_Their_Limits()
    {
        var data = new byte[520];
        data.AsSpan().Fill((byte)42);
        Assert.Equal(255, DeflateManager.CountMatchBytes(data.AsSpan(0, 255), data.AsSpan(1, 258)));
        Assert.Equal(
            0,
            DeflateManager.CountMatchBytes(data.AsSpan(0, 258), ReadOnlySpan<byte>.Empty)
        );
        data[256] = 43;
        Assert.Equal(255, DeflateManager.CountMatchBytes(data.AsSpan(0, 258), data.AsSpan(1, 258)));
    }
}
