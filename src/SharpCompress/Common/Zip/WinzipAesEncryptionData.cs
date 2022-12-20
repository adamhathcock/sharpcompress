#nullable disable

using System;
using System.Buffers.Binary;
using System.Security.Cryptography;

namespace SharpCompress.Common.Zip;

internal class WinzipAesEncryptionData
{
    private const int RFC2898_ITERATIONS = 1000;

    private readonly byte[] _salt;
    private readonly WinzipAesKeySize _keySize;
    private readonly byte[] _passwordVerifyValue;
    private readonly string _password;

    private byte[] _generatedVerifyValue;

    internal WinzipAesEncryptionData(
        WinzipAesKeySize keySize,
        byte[] salt,
        byte[] passwordVerifyValue,
        string password
    )
    {
        _keySize = keySize;
        _salt = salt;
        _passwordVerifyValue = passwordVerifyValue;
        _password = password;
        Initialize();
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

    private void Initialize()
    {
#if NET7_0
        var rfc2898 = new Rfc2898DeriveBytes(
            _password,
            _salt,
            RFC2898_ITERATIONS,
            HashAlgorithmName.SHA1
        );
#else
        var rfc2898 = new Rfc2898DeriveBytes(_password, _salt, RFC2898_ITERATIONS);
#endif

        KeyBytes = rfc2898.GetBytes(KeySizeInBytes); // 16 or 24 or 32 ???
        IvBytes = rfc2898.GetBytes(KeySizeInBytes);
        _generatedVerifyValue = rfc2898.GetBytes(2);

        var verify = BinaryPrimitives.ReadInt16LittleEndian(_passwordVerifyValue);
        if (_password != null)
        {
            var generated = BinaryPrimitives.ReadInt16LittleEndian(_generatedVerifyValue);
            if (verify != generated)
            {
                throw new InvalidFormatException("bad password");
            }
        }
    }
}
