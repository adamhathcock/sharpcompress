using System;
using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;

namespace SharpCompress.Common.Zip;

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
    {
        _keySize = keySize;

#if NETFRAMEWORK || NETSTANDARD2_0
        var rfc2898 = new Rfc2898DeriveBytes(password, salt, RFC2898_ITERATIONS);
        KeyBytes = rfc2898.GetBytes(KeySizeInBytes);
        IvBytes = rfc2898.GetBytes(KeySizeInBytes);
        var generatedVerifyValue = rfc2898.GetBytes(2);
#elif NET10_0_OR_GREATER
        var derivedKeySize = (KeySizeInBytes * 2) + 2;
        var passwordBytes = Encoding.UTF8.GetBytes(password);
        var derivedKey = Rfc2898DeriveBytes.Pbkdf2(
            passwordBytes,
            salt,
            RFC2898_ITERATIONS,
            HashAlgorithmName.SHA1,
            derivedKeySize
        );
        KeyBytes = derivedKey.AsSpan(0, KeySizeInBytes).ToArray();
        IvBytes = derivedKey.AsSpan(KeySizeInBytes, KeySizeInBytes).ToArray();
        var generatedVerifyValue = derivedKey.AsSpan((KeySizeInBytes * 2), 2).ToArray();
#else
        var rfc2898 = new Rfc2898DeriveBytes(
            password,
            salt,
            RFC2898_ITERATIONS,
            HashAlgorithmName.SHA1
        );
        KeyBytes = rfc2898.GetBytes(KeySizeInBytes);
        IvBytes = rfc2898.GetBytes(KeySizeInBytes);
        var generatedVerifyValue = rfc2898.GetBytes(2);
#endif

        var verify = BinaryPrimitives.ReadInt16LittleEndian(passwordVerifyValue);
        var generated = BinaryPrimitives.ReadInt16LittleEndian(generatedVerifyValue);
        if (verify != generated)
        {
            throw new InvalidFormatException("bad password");
        }
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
            _ => throw new InvalidOperationException(),
        };
}
