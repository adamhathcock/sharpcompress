using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using SharpCompress.Common.Rar.Headers;

namespace SharpCompress.Common.Rar;

internal class CryptKey5 : ICryptKey
{
    const int AES_256 = 256;
    const int DERIVED_KEY_LENGTH = 0x10;
    const int SHA256_DIGEST_SIZE = 32;

    private readonly string _password;
    private readonly Rar5CryptoInfo _cryptoInfo;
    private readonly Rar5KeyCache _keyCache;
    private Rar5DerivedKey? _derivedKey;

    /// <param name="keyCache">
    /// Cache shared by the keys of one volume so that encrypted headers and files derive the
    /// PBKDF2 key once. When null, the key still reuses its own derivation across calls.
    /// </param>
    public CryptKey5(string? password, Rar5CryptoInfo rar5CryptoInfo, Rar5KeyCache? keyCache = null)
    {
        _password = password ?? "";
        _cryptoInfo = rar5CryptoInfo;
        _keyCache = keyCache ?? new Rar5KeyCache();
    }

    public byte[] PswCheck => _derivedKey?.PswCheck ?? [];

    public byte[] HashKey => _derivedKey?.HashKey ?? [];

    private static List<byte[]> GenerateRarPBKDF2Key(
        string password,
        byte[] salt,
        int iterations,
        int keyLength
    )
    {
        var passwordBytes = Encoding.UTF8.GetBytes(password);
#if LEGACY_DOTNET
        using var hmac = new HMACSHA256(passwordBytes);
        var block = hmac.ComputeHash(salt);
#else
        var block = HMACSHA256.HashData(passwordBytes, salt);
#endif
        var finalHash = (byte[])block.Clone();

        var loop = new int[] { iterations, 17, 17 };
        var res = new List<byte[]> { };

        for (var x = 0; x < 3; x++)
        {
            for (var i = 1; i < loop[x]; i++)
            {
#if LEGACY_DOTNET
                block = hmac.ComputeHash(block);
#else
                block = HMACSHA256.HashData(passwordBytes, block);
#endif
                for (var j = 0; j < finalHash.Length; j++)
                {
                    finalHash[j] ^= block[j];
                }
            }

            res.Add((byte[])finalHash.Clone());
        }

        return res;
    }

    private static Rar5DerivedKey DeriveKey(string password, byte[] salt, int lg2Count)
    {
        var derivedKey = GenerateRarPBKDF2Key(
            password,
            salt.Concat(new byte[] { 0, 0, 0, 1 }).ToArray(),
            1 << lg2Count,
            DERIVED_KEY_LENGTH
        );
        var pswCheck = new byte[EncryptionConstV5.SIZE_PSWCHECK];
        for (var i = 0; i < SHA256_DIGEST_SIZE; i++)
        {
            pswCheck[i % EncryptionConstV5.SIZE_PSWCHECK] ^= derivedKey[2][i];
        }
        return new Rar5DerivedKey(derivedKey[0], derivedKey[1], pswCheck);
    }

    public ICryptoTransform Transformer(byte[] salt)
    {
        // IVs change for each header block and file, but the expensive KDF inputs do not.
        _derivedKey = _keyCache.GetOrDerive(_password, salt, _cryptoInfo.LG2Count, DeriveKey);

        if (_cryptoInfo.UsePswCheck && !_cryptoInfo.PswCheck.SequenceEqual(_derivedKey.PswCheck))
        {
            throw new CryptographicException("The password did not match.");
        }

        using var aes = Aes.Create();
        aes.KeySize = AES_256;
        aes.Mode = CipherMode.CBC;
        aes.Padding = PaddingMode.None;
        aes.Key = _derivedKey.AesKey;
        aes.IV = _cryptoInfo.InitV;
        return aes.CreateDecryptor();
    }
}
