using System;
using System.IO;
using SharpCompress.Compressors.LZMA;
using SharpCompress.Compressors.LZMA.Utilities;
using Xunit;

namespace SharpCompress.Test.SevenZip;

public class SevenZipAesKdfTests
{
    [Theory]
    [InlineData(25)]
    [InlineData(31)]
    [InlineData(62)]
    public void SevenZipAes_Rejects_Excessive_Kdf_Without_Salt(int exponent)
    {
        using var input = new MemoryStream();
        Assert.Throws<NotSupportedException>(() =>
            new AesDecoderStream(input, [(byte)exponent], new Password(), 0)
        );
    }

    [Fact]
    public void SevenZipAes_Allows_Direct_Key_Without_Iv()
    {
        using var input = new MemoryStream();
        using var decoder = new AesDecoderStream(input, [0x3F], new Password(), 0);
        Assert.Equal(0, decoder.Read(new byte[1], 0, 1));
    }

    private sealed class Password : IPasswordProvider
    {
        public string CryptoGetTextPassword() => "test";
    }
}
