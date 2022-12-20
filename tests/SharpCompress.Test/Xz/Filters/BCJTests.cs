/*
 * BCJTests.cs -- XZ converter test class
 * <Contribution by Louis-Michel Bergeron, on behalf of aDolus Technolog Inc., 2022>
 */

using System.IO;
using SharpCompress.Compressors.Xz.Filters;
using Xunit;

namespace SharpCompress.Test.Xz.Filters;

public class BCJTests : XZTestsBase
{
    private readonly ArmFilter armFilter;
    private readonly ArmThumbFilter armtFilter;
    private readonly IA64Filter ia64Filter;
    private readonly PowerPCFilter ppcFilter;
    private readonly SparcFilter sparcFilter;
    private readonly X86Filter x86Filter;

    public BCJTests()
    {
        armFilter = new ArmFilter();
        armtFilter = new ArmThumbFilter();
        ia64Filter = new IA64Filter();
        ppcFilter = new PowerPCFilter();
        sparcFilter = new SparcFilter();
        x86Filter = new X86Filter();
    }

    [Fact]
    public void IsOnlyAllowedLast()
    {
        Assert.False(armFilter.AllowAsLast);
        Assert.True(armFilter.AllowAsNonLast);

        Assert.False(armtFilter.AllowAsLast);
        Assert.True(armtFilter.AllowAsNonLast);

        Assert.False(ia64Filter.AllowAsLast);
        Assert.True(ia64Filter.AllowAsNonLast);

        Assert.False(ppcFilter.AllowAsLast);
        Assert.True(ppcFilter.AllowAsNonLast);

        Assert.False(sparcFilter.AllowAsLast);
        Assert.True(sparcFilter.AllowAsNonLast);

        Assert.False(x86Filter.AllowAsLast);
        Assert.True(x86Filter.AllowAsNonLast);
    }

    [Fact]
    public void ChangesStreamSize()
    {
        Assert.False(armFilter.ChangesDataSize);
        Assert.False(armtFilter.ChangesDataSize);
        Assert.False(ia64Filter.ChangesDataSize);
        Assert.False(ppcFilter.ChangesDataSize);
        Assert.False(sparcFilter.ChangesDataSize);
        Assert.False(x86Filter.ChangesDataSize);
    }

    [Theory]
    [InlineData(new byte[] { 0 })]
    [InlineData(new byte[] { 0, 0, 0, 0, 0 })]
    public void OnlyAcceptsOneByte(byte[] bytes)
    {
        InvalidDataException ex;
        ex = Assert.Throws<InvalidDataException>(() => armFilter.Init(bytes));
        Assert.Equal("ARM properties unexpected length", ex.Message);

        ex = Assert.Throws<InvalidDataException>(() => armtFilter.Init(bytes));
        Assert.Equal("ARM Thumb properties unexpected length", ex.Message);

        ex = Assert.Throws<InvalidDataException>(() => ia64Filter.Init(bytes));
        Assert.Equal("IA64 properties unexpected length", ex.Message);

        ex = Assert.Throws<InvalidDataException>(() => ppcFilter.Init(bytes));
        Assert.Equal("PPC properties unexpected length", ex.Message);

        ex = Assert.Throws<InvalidDataException>(() => sparcFilter.Init(bytes));
        Assert.Equal("SPARC properties unexpected length", ex.Message);

        ex = Assert.Throws<InvalidDataException>(() => x86Filter.Init(bytes));
        Assert.Equal("X86 properties unexpected length", ex.Message);
    }
}
