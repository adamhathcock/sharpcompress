using System;
using System.Buffers.Binary;
using System.Security.Cryptography;

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
#else
        var rfc2898 = new Rfc2898DeriveBytes(
            password,
            salt,
            RFC2898_ITERATIONS,
            HashAlgorithmName.SHA1
        );
#endif

        KeyBytes = rfc2898.GetBytes(KeySizeInBytes); // 16 or 24 or 32 ???
        IvBytes = rfc2898.GetBytes(KeySizeInBytes);
        var generatedVerifyValue = rfc2898.GetBytes(2);

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
