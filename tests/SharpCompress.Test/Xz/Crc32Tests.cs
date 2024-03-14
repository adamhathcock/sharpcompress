using System.Text;
using SharpCompress.Compressors.Xz;
using Xunit;

namespace SharpCompress.Test.Xz;

public class Crc32Tests
{
    private const string SIMPLE_STRING = @"The quick brown fox jumps over the lazy dog.";
    private readonly byte[] _simpleBytes = Encoding.ASCII.GetBytes(SIMPLE_STRING);
    private const string SIMPLE_STRING2 =
        @"Life moves pretty fast. If you don't stop and look around once in a while, you could miss it.";
    private readonly byte[] _simpleBytes2 = Encoding.ASCII.GetBytes(SIMPLE_STRING2);

    [Fact]
    public void ShortAsciiString()
    {
        var actual = Crc32.Compute(_simpleBytes);

        Assert.Equal((uint)0x519025e9, actual);
    }

    [Fact]
    public void ShortAsciiString2()
    {
        var actual = Crc32.Compute(_simpleBytes2);

        Assert.Equal((uint)0x6ee3ad88, actual);
    }
}
