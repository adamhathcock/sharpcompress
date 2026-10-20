using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using SharpCompress.Common.Rar;
using SharpCompress.Common.Rar.Headers;
using SharpCompress.IO;
using SharpCompress.Readers;
using Xunit;

namespace SharpCompress.Test.Rar;

public class CryptKey5Tests : TestBase
{
    [Theory]
    [InlineData("", 1)]
    [InlineData("password", 2)]
    [InlineData("password", 4096)]
    [InlineData("pässwörd🔐", 32)]
    [InlineData(
        "a password longer than the SHA256 block size of sixty-four bytes to exercise HMAC key hashing",
        128
    )]
    public void Rar5_DerivedKeys_MatchPbkdf2_Snapshots(string password, int iterations)
    {
        var salt = Enumerable.Range(0, 16).Select(i => (byte)i).ToArray();
        var originalSalt = (byte[])salt.Clone();
        var keys = CryptKey5.GenerateRarPBKDF2Key(password, salt, iterations);

        Assert.Equal(3, keys.Count);
        for (var i = 0; i < keys.Count; i++)
        {
#if LEGACY_DOTNET
            using var reference = new Rfc2898DeriveBytes(
                Encoding.UTF8.GetBytes(password),
                salt,
                iterations + i * 16,
                HashAlgorithmName.SHA256
            );
            Assert.Equal(reference.GetBytes(32), keys[i]);
#else
            var expected = Rfc2898DeriveBytes.Pbkdf2(
                Encoding.UTF8.GetBytes(password),
                salt,
                iterations + i * 16,
                HashAlgorithmName.SHA256,
                32
            );
            Assert.Equal(expected, keys[i]);
#endif
        }
        Assert.Equal(originalSalt, salt);
    }

#if !LEGACY_DOTNET
    [Fact]
    public void Rar5_DerivedKeys_DoNotAllocate_PerIteration()
    {
        var salt = new byte[16];
        CryptKey5.GenerateRarPBKDF2Key("password", salt, 4096);
        var before = GC.GetAllocatedBytesForCurrentThread();
        CryptKey5.GenerateRarPBKDF2Key("password", salt, 4096);
        var allocated = GC.GetAllocatedBytesForCurrentThread() - before;

        // Allow fixed setup costs, but not a digest array for each iteration.
        Assert.True(allocated < 16 * 1024, $"Allocated {allocated} bytes.");
    }
#endif

    [Theory]
    [InlineData(0)]
    [InlineData(24)]
    [InlineData(25)]
    [InlineData(30)]
    [InlineData(31)]
    [InlineData(32)]
    [InlineData(36)]
    [InlineData(128)]
    [InlineData(255)]
    public async Task Rar5_CryptoInfo_Validates_Kdf_Count(int lg2Count)
    {
        // The KDF count is one raw byte, not a vint. Test the upper bound without deriving a key.
        var data = new byte[35];
        data[2] = (byte)lg2Count;
        using var syncStream = new MemoryStream(data);
        using var asyncStream = new MemoryStream(data);
        using var syncReader = new MarkingBinaryReader(syncStream);
        using var asyncReader = new AsyncMarkingBinaryReader(asyncStream);
        if (lg2Count <= 24)
        {
            Assert.Equal(lg2Count, Rar5CryptoInfo.Create(syncReader, true).LG2Count);
            Assert.Equal(lg2Count, (await Rar5CryptoInfo.CreateAsync(asyncReader, true)).LG2Count);
        }
        else
        {
            Assert.Throws<SharpCompress.Common.CryptographicException>(() =>
                Rar5CryptoInfo.Create(syncReader, true)
            );
            await Assert.ThrowsAsync<SharpCompress.Common.CryptographicException>(async () =>
                await Rar5CryptoInfo.CreateAsync(asyncReader, true)
            );
            Assert.Equal(3, syncStream.Position);
            Assert.Equal(3, asyncStream.Position);
        }
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(25)]
    [InlineData(30)]
    [InlineData(31)]
    [InlineData(32)]
    [InlineData(33)]
    [InlineData(34)]
    [InlineData(35)]
    [InlineData(36)]
    [InlineData(255)]
    [InlineData(int.MaxValue)]
    public void Rar5_KeyCache_Rejects_Invalid_Kdf_Count(int lg2Count)
    {
        var cache = new Rar5KeyCache();
        Assert.Throws<SharpCompress.Common.CryptographicException>(() =>
            cache.GetKeys("test", new byte[16], lg2Count)
        );
    }

    [Fact]
    public void Rar5_Key_Rejects_Mutated_Kdf_Count()
    {
        var info = ReadCryptoInfo();
        var key = new CryptKey5("test", info);
        using var transform = key.Transformer(info.Salt);
        info.LG2Count = 32;

        Assert.Throws<SharpCompress.Common.CryptographicException>(() =>
            key.Transformer(info.Salt)
        );
    }

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
    public void Rar5_Key_SharesDerivedMaterial_AcrossEntries()
    {
        var cache = new Rar5KeyCache();
        var firstInfo = ReadCryptoInfo();
        var secondInfo = ReadCryptoInfo();
        secondInfo.InitV[0] = 1;
        var first = new CryptKey5("test", firstInfo, cache);
        var second = new CryptKey5("test", secondInfo, cache);
        using var firstTransform = first.Transformer(firstInfo.Salt);
        using var secondTransform = second.Transformer(secondInfo.Salt);
        Assert.Same(first.HashKey, second.HashKey);
        Assert.Equal(first.PswCheck, second.PswCheck);
        var firstBlock = firstTransform.TransformFinalBlock(new byte[16], 0, 16);
        var secondBlock = secondTransform.TransformFinalBlock(new byte[16], 0, 16);
        Assert.False(firstBlock.SequenceEqual(secondBlock));
        using var fresh = new CryptKey5("test", secondInfo).Transformer(secondInfo.Salt);
        Assert.Equal(fresh.TransformFinalBlock(new byte[16], 0, 16), secondBlock);

        secondInfo.PswCheck[0] ^= 1;
        Assert.Throws<SharpCompress.Common.CryptographicException>(() =>
            new CryptKey5("test", secondInfo, cache).Transformer(secondInfo.Salt)
        );
        Assert.Throws<SharpCompress.Common.CryptographicException>(() =>
            new CryptKey5("failed", firstInfo, cache).Transformer(firstInfo.Salt)
        );
    }

    [Fact]
    public void Rar5_KeyCache_MatchesInputs_AndBoundsRetention()
    {
        var cache = new Rar5KeyCache();
        var salt = new byte[16];
        var original = cache.GetKeys("test", salt, 0);
        Assert.Same(original, cache.GetKeys("test", (byte[])salt.Clone(), 0));
        Assert.NotSame(original, cache.GetKeys("other", salt, 0));
        Assert.NotSame(original, cache.GetKeys("test", salt, 1));
        salt[0] = 1;
        Assert.NotSame(original, cache.GetKeys("test", salt, 0));
        Assert.Same(original, cache.GetKeys("test", new byte[16], 0));
        salt[0] = 2;
        cache.GetKeys("test", salt, 0);
        Assert.NotSame(original, cache.GetKeys("test", new byte[16], 0));
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
}
