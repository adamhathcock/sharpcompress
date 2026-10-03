using System.IO;
using System.Linq;
using SharpCompress.Common.Rar;
using SharpCompress.Common.Rar.Headers;
using SharpCompress.IO;
using SharpCompress.Readers;
using Xunit;

namespace SharpCompress.Test.Rar;

public class CryptKey5Tests : TestBase
{
    private Rar5CryptoInfo ReadCryptoInfo()
    {
        using var stream = File.OpenRead(
            Path.Combine(TEST_ARCHIVES_PATH, "Rar5.encrypted_filesAndHeader.rar")
        );
        var factory = new RarHeaderFactory(StreamingMode.Seekable, ReaderOptions.ForExternalStream);
        var info = factory.ReadHeaders(stream).OfType<ArchiveCryptHeader>().First().CryptInfo;
        info.InitV = new byte[16];
        return info;
    }

    [Fact]
    public void Rar5_Key_ReusesDerivedMaterial_WithDifferentIVs()
    {
        var info = ReadCryptoInfo();
        var key = new CryptKey5("test", info);
        using var first = key.Transformer(info.Salt);
        var firstBlock = first.TransformFinalBlock(new byte[16], 0, 16);
        var hashKey = key.HashKey;
        var passwordCheck = key.PswCheck;

        info.InitV[0] = 1;
        using var second = key.Transformer((byte[])info.Salt.Clone());
        var secondBlock = second.TransformFinalBlock(new byte[16], 0, 16);
        Assert.Same(hashKey, key.HashKey);
        Assert.Same(passwordCheck, key.PswCheck);
        Assert.False(firstBlock.SequenceEqual(secondBlock));

        using var fresh = new CryptKey5("test", info).Transformer(info.Salt);
        Assert.Equal(fresh.TransformFinalBlock(new byte[16], 0, 16), secondBlock);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Rar5_Key_InvalidatesDerivedMaterial_WhenInputsChange(bool changeIterations)
    {
        var info = ReadCryptoInfo();
        var key = new CryptKey5("test", info);
        using var first = key.Transformer(info.Salt);
        var hashKey = key.HashKey;
        info.UsePswCheck = false;
        if (changeIterations)
        {
            info.LG2Count--;
        }
        else
        {
            info.Salt[0] ^= 1;
        }

        using var second = key.Transformer(info.Salt);
        using var fresh = new CryptKey5("test", info).Transformer(info.Salt);
        Assert.NotSame(hashKey, key.HashKey);
        Assert.Equal(
            fresh.TransformFinalBlock(new byte[16], 0, 16),
            second.TransformFinalBlock(new byte[16], 0, 16)
        );
    }

    [Fact]
    public void Rar5_Key_ValidatesPasswordCheck_OnCacheHit()
    {
        var info = ReadCryptoInfo();
        var key = new CryptKey5("test", info);
        using var first = key.Transformer(info.Salt);
        info.PswCheck[0] ^= 1;
        Assert.Throws<SharpCompress.Common.CryptographicException>(() =>
            key.Transformer(info.Salt)
        );
    }

    [Fact]
    public void Rar5_Keys_ShareDerivedMaterial_ThroughCache()
    {
        var cache = new Rar5KeyCache();
        var firstInfo = ReadCryptoInfo();
        var secondInfo = ReadCryptoInfo();
        secondInfo.InitV[0] = 1;

        using var first = new CryptKey5("test", firstInfo, cache).Transformer(firstInfo.Salt);
        using var second = new CryptKey5("test", secondInfo, cache).Transformer(secondInfo.Salt);
        Assert.Equal(1, cache.DerivationCount);

        using var fresh = new CryptKey5("test", secondInfo).Transformer(secondInfo.Salt);
        Assert.Equal(
            fresh.TransformFinalBlock(new byte[16], 0, 16),
            second.TransformFinalBlock(new byte[16], 0, 16)
        );
    }

    [Fact]
    public void Rar5_KeyCache_SeparatesPasswords()
    {
        var cache = new Rar5KeyCache();
        var info = ReadCryptoInfo();
        using var first = new CryptKey5("test", info, cache).Transformer(info.Salt);
        Assert.Throws<SharpCompress.Common.CryptographicException>(() =>
            new CryptKey5("failed", info, cache).Transformer(info.Salt)
        );
        Assert.Equal(2, cache.DerivationCount);
    }
}
