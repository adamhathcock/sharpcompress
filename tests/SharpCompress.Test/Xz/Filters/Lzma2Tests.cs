using System;
using SharpCompress.Common;
using SharpCompress.Compressors.Xz.Filters;
using Xunit;

namespace SharpCompress.Test.Xz.Filters;

public class Lzma2Tests : XzTestsBase
{
    private readonly Lzma2Filter _filter;

    public Lzma2Tests() => _filter = new Lzma2Filter();

    [Fact]
    public void IsOnlyAllowedLast()
    {
        Assert.True(_filter.AllowAsLast);
        Assert.False(_filter.AllowAsNonLast);
    }

    [Fact]
    public void ChangesStreamSize() => Assert.True(_filter.ChangesDataSize);

    [Theory]
    [InlineData(0, (uint)4 * 1024)]
    [InlineData(1, (uint)6 * 1024)]
    [InlineData(2, (uint)8 * 1024)]
    [InlineData(3, (uint)12 * 1024)]
    [InlineData(38, (uint)2 * 1024 * 1024 * 1024)]
    [InlineData(39, (uint)3 * 1024 * 1024 * 1024)]
    [InlineData(40, (uint)(1024 * 1024 * 1024 - 1) * 4 + 3)]
    public void CalculatesDictionarySize(byte inByte, uint dicSize)
    {
        _filter.Init([inByte]);
        Assert.Equal(_filter.DictionarySize, dicSize);
    }

    [Fact]
    public void CalculatesDictionarySizeError()
    {
        uint temp;
        _filter.Init([41]);
        var ex = Assert.Throws<OverflowException>(() =>
        {
            temp = _filter.DictionarySize;
        });
        Assert.Equal("Dictionary size greater than UInt32.Max", ex.Message);
    }

    [Theory]
    [InlineData(new byte[] { })]
    [InlineData(new byte[] { 0, 0 })]
    public void OnlyAcceptsOneByte(byte[] bytes)
    {
        var ex = Assert.Throws<InvalidFormatException>(() => _filter.Init(bytes));
        Assert.Equal("LZMA properties unexpected length", ex.Message);
    }

    [Fact]
    public void ReservedBytesThrow()
    {
        var ex = Assert.Throws<InvalidFormatException>(() => _filter.Init([0xC0]));
        Assert.Equal("Reserved bits used in LZMA properties", ex.Message);
    }
}
