using System;
using System.Buffers.Binary;
using System.Diagnostics.CodeAnalysis;
using System.Security.Cryptography;
using System.Text;

namespace SharpCompress.Common.Zip;

[SuppressMessage(
    "Security",
    "CA5379:Rfc2898DeriveBytes might be using a weak hash algorithm",
    Justification = "WinZip AES specification requires PBKDF2 with SHA-1."
)]
internal class WinzipAesEncryptionData
{
    private const int RFC2898_ITERATIONS = 1000;

    private readonly WinzipAesKeySize _keySize;

    internal WinzipAesEncryptionData(
        WinzipAesKeySize keySize,
        byte[] salt,
        byte[] passwordVerifyValue,
        string password
    )
        : this(keySize, DeriveKey(keySize, salt, password), passwordVerifyValue) { }

    /// <summary>
    /// Builds the encryption data from key material derived ahead of time by <see cref="DeriveKey"/>.
    /// </summary>
    internal WinzipAesEncryptionData(
        WinzipAesKeySize keySize,
        byte[] derivedKey,
        byte[] passwordVerifyValue
    )
    {
        _keySize = keySize;
        KeyBytes = derivedKey.AsSpan(0, KeySizeInBytes).ToArray();
        IvBytes = derivedKey.AsSpan(KeySizeInBytes, KeySizeInBytes).ToArray();
        var generatedVerifyValue = derivedKey.AsSpan(KeySizeInBytes * 2, 2).ToArray();

        var verify = BinaryPrimitives.ReadInt16LittleEndian(passwordVerifyValue);
        var generated = BinaryPrimitives.ReadInt16LittleEndian(generatedVerifyValue);
        if (verify != generated)
        {
            throw new InvalidFormatException("bad password");
        }
    }

    /// <summary>
    /// Derives the encryption key, authentication key and password verifier (PBKDF2-SHA1, 1000 iterations).
    /// This is the dominant per-entry cost and is safe to run on any thread.
    /// </summary>
    internal static byte[] DeriveKey(WinzipAesKeySize keySize, byte[] salt, string password)
    {
        var keyLength = KeyLengthInBytes(keySize);

#if LEGACY_DOTNET
        var rfc2898 = new Rfc2898DeriveBytes(password, salt, RFC2898_ITERATIONS);
        return rfc2898.GetBytes((keyLength * 2) + 2);
#elif NET10_0_OR_GREATER
        return Rfc2898DeriveBytes.Pbkdf2(
            Encoding.UTF8.GetBytes(password),
            salt,
            RFC2898_ITERATIONS,
            HashAlgorithmName.SHA1,
            (keyLength * 2) + 2
        );
#else
        using var rfc2898 = new Rfc2898DeriveBytes(
            password,
            salt,
            RFC2898_ITERATIONS,
            HashAlgorithmName.SHA1
        );
        return rfc2898.GetBytes((keyLength * 2) + 2);
#endif
    }

    internal byte[] IvBytes { get; set; }

    internal byte[] KeyBytes { get; set; }

    private int KeySizeInBytes => KeyLengthInBytes(_keySize);

    internal static int KeyLengthInBytes(WinzipAesKeySize keySize) =>
        keySize switch
        {
            WinzipAesKeySize.KeySize128 => 16,
            WinzipAesKeySize.KeySize192 => 24,
            WinzipAesKeySize.KeySize256 => 32,
            _ => throw new ArchiveOperationException(),
        };
}
