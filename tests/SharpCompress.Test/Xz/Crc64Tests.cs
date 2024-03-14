using System.Text;
using SharpCompress.Compressors.Xz;
using Xunit;

namespace SharpCompress.Test.Xz;

public class Crc64Tests
{
    private const string SIMPLE_STRING = @"The quick brown fox jumps over the lazy dog.";
    private readonly byte[] _simpleBytes = Encoding.ASCII.GetBytes(SIMPLE_STRING);
    private const string SIMPLE_STRING2 =
        @"Life moves pretty fast. If you don't stop and look around once in a while, you could miss it.";
    private readonly byte[] _simpleBytes2 = Encoding.ASCII.GetBytes(SIMPLE_STRING2);

    [Fact]
    public void ShortAsciiString()
    {
        var actual = Crc64.Compute(_simpleBytes);

        Assert.Equal((ulong)0x7E210EB1B03E5A1D, actual);
    }

    [Fact]
    public void ShortAsciiString2()
    {
        var actual = Crc64.Compute(_simpleBytes2);

        Assert.Equal((ulong)0x416B4150508661EE, actual);
    }
}
