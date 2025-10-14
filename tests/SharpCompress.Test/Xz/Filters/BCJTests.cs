/*
 * BCJTests.cs -- XZ converter test class
 * <Contribution by Louis-Michel Bergeron, on behalf of aDolus Technolog Inc., 2022>
 */

using SharpCompress.Common;
using SharpCompress.Compressors.Xz.Filters;
using Xunit;

namespace SharpCompress.Test.Xz.Filters;

public class BcjTests : XzTestsBase
{
    private readonly ArmFilter _armFilter;
    private readonly ArmThumbFilter _armtFilter;
    private readonly IA64Filter _ia64Filter;
    private readonly PowerPCFilter _ppcFilter;
    private readonly SparcFilter _sparcFilter;
    private readonly X86Filter _x86Filter;

    public BcjTests()
    {
        _armFilter = new ArmFilter();
        _armtFilter = new ArmThumbFilter();
        _ia64Filter = new IA64Filter();
        _ppcFilter = new PowerPCFilter();
        _sparcFilter = new SparcFilter();
        _x86Filter = new X86Filter();
    }

    [Fact]
    public void IsOnlyAllowedLast()
    {
        Assert.False(_armFilter.AllowAsLast);
        Assert.True(_armFilter.AllowAsNonLast);

        Assert.False(_armtFilter.AllowAsLast);
        Assert.True(_armtFilter.AllowAsNonLast);

        Assert.False(_ia64Filter.AllowAsLast);
        Assert.True(_ia64Filter.AllowAsNonLast);

        Assert.False(_ppcFilter.AllowAsLast);
        Assert.True(_ppcFilter.AllowAsNonLast);

        Assert.False(_sparcFilter.AllowAsLast);
        Assert.True(_sparcFilter.AllowAsNonLast);

        Assert.False(_x86Filter.AllowAsLast);
        Assert.True(_x86Filter.AllowAsNonLast);
    }

    [Fact]
    public void ChangesStreamSize()
    {
        Assert.False(_armFilter.ChangesDataSize);
        Assert.False(_armtFilter.ChangesDataSize);
        Assert.False(_ia64Filter.ChangesDataSize);
        Assert.False(_ppcFilter.ChangesDataSize);
        Assert.False(_sparcFilter.ChangesDataSize);
        Assert.False(_x86Filter.ChangesDataSize);
    }

    [Theory]
    [InlineData(new byte[] { 0 })]
    [InlineData(new byte[] { 0, 0, 0, 0, 0 })]
    public void OnlyAcceptsOneByte(byte[] bytes)
    {
        InvalidFormatException ex;
        ex = Assert.Throws<InvalidFormatException>(() => _armFilter.Init(bytes));
        Assert.Equal("ARM properties unexpected length", ex.Message);

        ex = Assert.Throws<InvalidFormatException>(() => _armtFilter.Init(bytes));
        Assert.Equal("ARM Thumb properties unexpected length", ex.Message);

        ex = Assert.Throws<InvalidFormatException>(() => _ia64Filter.Init(bytes));
        Assert.Equal("IA64 properties unexpected length", ex.Message);

        ex = Assert.Throws<InvalidFormatException>(() => _ppcFilter.Init(bytes));
        Assert.Equal("PPC properties unexpected length", ex.Message);

        ex = Assert.Throws<InvalidFormatException>(() => _sparcFilter.Init(bytes));
        Assert.Equal("SPARC properties unexpected length", ex.Message);

        ex = Assert.Throws<InvalidFormatException>(() => _x86Filter.Init(bytes));
        Assert.Equal("X86 properties unexpected length", ex.Message);
    }
}
