using System;
using System.IO;
using System.Text;
using SharpCompress.Compressors.Deflate;
using Xunit;

namespace SharpCompress.Test;

public class DeflateCrc32Tests
{
    [Theory]
    [InlineData("", 0u)]
    [InlineData("123456789", 0xCBF43926u)]
    [InlineData("The quick brown fox jumps over the lazy dog", 0x414FA339u)]
    public void Known_Vectors(string text, uint expected)
    {
        var data = Encoding.ASCII.GetBytes(text);
        var crc = new CRC32();
        crc.SlurpBlock(data, 0, data.Length);
        Assert.Equal(expected, unchecked((uint)crc.Crc32Result));
        Assert.Equal(data.Length, crc.TotalBytesRead);
    }

    [Fact]
    public void Offsets_Tails_And_Chunking_Match_Reference()
    {
        var data = new byte[65553];
        new Random(12345).NextBytes(data);
        foreach (var offset in new[] { 0, 1, 3, 7 })
        {
            foreach (var length in new[] { 0, 1, 7, 8, 9, 15, 16, 17, 255, 256, 257, 65536 })
            {
                var expected = Reference(data, offset, length);
                var whole = new CRC32();
                whole.SlurpBlock(data, offset, length);
                Assert.Equal(expected, unchecked((uint)whole.Crc32Result));
                Assert.Equal(length, whole.TotalBytesRead);
                foreach (var chunk in new[] { 1, 7, 8, 13, 1024 })
                {
                    var crc = new CRC32();
                    for (var pos = 0; pos < length; pos += chunk)
                    {
                        crc.SlurpBlock(data, offset + pos, Math.Min(chunk, length - pos));
                    }
                    crc.SlurpBlock(data, offset, 0);
                    Assert.Equal(expected, unchecked((uint)crc.Crc32Result));
                    Assert.Equal(length, crc.TotalBytesRead);
                }
            }
        }
    }

    [Fact]
    public void Combine_And_Continue_Match_Reference()
    {
        var data = new byte[4097];
        new Random(6789).NextBytes(data);
        foreach (var split in new[] { 0, 1, 7, 8, 1023, 4096 })
        {
            var prefix = new CRC32();
            prefix.SlurpBlock(data, 0, split);
            var suffix = new CRC32();
            suffix.SlurpBlock(data, split, data.Length - split);
            prefix.Combine(suffix.Crc32Result, data.Length - split);
            Assert.Equal(Reference(data, 0, data.Length), unchecked((uint)prefix.Crc32Result));
            prefix.Combine(0, 0);
            prefix.SlurpBlock(data, 0, 17);
            var reference = new CRC32();
            reference.SlurpBlock(data, 0, data.Length);
            reference.SlurpBlock(data, 0, 17);
            Assert.Equal(reference.Crc32Result, prefix.Crc32Result);
        }
    }

    [Fact]
    public void Stream_Copy_And_Bytewise_Update_Match_Reference()
    {
        var data = new byte[20001];
        new Random(42).NextBytes(data);
        using var input = new MemoryStream(data);
        using var copy = new MemoryStream();
        var crc = new CRC32();
        Assert.Equal(Reference(data, 0, data.Length), crc.GetCrc32AndCopy(input, copy));
        Assert.Equal(data, copy.ToArray());
        uint running = uint.MaxValue;
        foreach (var b in data)
        {
            running = unchecked((uint)crc.ComputeCrc32(unchecked((int)running), b));
        }
        Assert.Equal(Reference(data, 0, data.Length), ~running);
    }

    private static uint Reference(byte[] data, int offset, int length)
    {
        uint crc = uint.MaxValue;
        for (var i = 0; i < length; i++)
        {
            crc ^= data[offset + i];
            for (var bit = 0; bit < 8; bit++)
            {
                crc = (crc >> 1) ^ ((crc & 1) != 0 ? 0xEDB88320u : 0u);
            }
        }
        return ~crc;
    }
}
