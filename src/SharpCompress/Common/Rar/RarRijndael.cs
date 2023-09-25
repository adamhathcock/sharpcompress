using System;
using System.Security.Cryptography;
using System.Text;
using SharpCompress.Crypto;

namespace SharpCompress.Common.Rar;

internal class RarRijndael : IDisposable
{
    internal const int CRYPTO_BLOCK_SIZE = 16;

    private readonly string _password;
    private readonly byte[] _salt;
    private byte[]? _aesInitializationVector;
    private RijndaelEngine? _rijndael;

    private RarRijndael(string password, byte[] salt)
    {
        _password = password;
        _salt = salt;
    }

    private void Initialize()
    {
        _rijndael = new RijndaelEngine();
        _aesInitializationVector = new byte[CRYPTO_BLOCK_SIZE];
        var rawLength = 2 * _password.Length;
        var rawPassword = new byte[rawLength + 8];
        var passwordBytes = Encoding.UTF8.GetBytes(_password);
        for (var i = 0; i < _password.Length; i++)
        {
            rawPassword[i * 2] = passwordBytes[i];
            rawPassword[(i * 2) + 1] = 0;
        }
        for (var i = 0; i < _salt.Length; i++)
        {
            rawPassword[i + rawLength] = _salt[i];
        }

        const int noOfRounds = (1 << 18);
        const int iblock = 3;
        byte[] digest;
        var data = new byte[(rawPassword.Length + iblock) * noOfRounds];

        //TODO slow code below, find ways to optimize
        for (var i = 0; i < noOfRounds; i++)
        {
            rawPassword.CopyTo(data, i * (rawPassword.Length + iblock));

            data[(i * (rawPassword.Length + iblock)) + rawPassword.Length + 0] = (byte)i;
            data[(i * (rawPassword.Length + iblock)) + rawPassword.Length + 1] = (byte)(i >> 8);
            data[(i * (rawPassword.Length + iblock)) + rawPassword.Length + 2] = (byte)(
                i >> CRYPTO_BLOCK_SIZE
            );

            if (i % (noOfRounds / CRYPTO_BLOCK_SIZE) == 0)
            {
                digest = SHA1.Create()
                    .ComputeHash(data, 0, (i + 1) * (rawPassword.Length + iblock));
                _aesInitializationVector[i / (noOfRounds / CRYPTO_BLOCK_SIZE)] = digest[19];
            }
        }
        digest = SHA1.Create().ComputeHash(data);
        //slow code ends

        var aesKey = new byte[CRYPTO_BLOCK_SIZE];
        for (var i = 0; i < 4; i++)
        {
            for (var j = 0; j < 4; j++)
            {
                aesKey[(i * 4) + j] = (byte)(
                    (
                        ((digest[i * 4] * 0x1000000) & 0xff000000)
                        | (uint)((digest[(i * 4) + 1] * 0x10000) & 0xff0000)
                        | (uint)((digest[(i * 4) + 2] * 0x100) & 0xff00)
                        | (uint)(digest[(i * 4) + 3] & 0xff)
                    ) >> (j * 8)
                );
            }
        }

        _rijndael.Init(false, new KeyParameter(aesKey));
    }

    public static RarRijndael InitializeFrom(string password, byte[] salt)
    {
        var rijndael = new RarRijndael(password, salt);
        rijndael.Initialize();
        return rijndael;
    }

    public byte[] ProcessBlock(ReadOnlySpan<byte> cipherText)
    {
        if (_rijndael is null || _aesInitializationVector is null)
        {
            throw new InvalidOperationException("Initialize not called.");
        }
        Span<byte> plainText = stackalloc byte[CRYPTO_BLOCK_SIZE]; // 16 bytes
        var decryptedBytes = new byte[CRYPTO_BLOCK_SIZE];
        _rijndael.ProcessBlock(cipherText, plainText);

        for (var j = 0; j < CRYPTO_BLOCK_SIZE; j++)
        {
            decryptedBytes[j] = (byte)(plainText[j] ^ _aesInitializationVector[j % 16]); //32:114, 33:101
        }

        for (var j = 0; j < _aesInitializationVector.Length; j++)
        {
            _aesInitializationVector[j] = cipherText[j];
        }

        return decryptedBytes;
    }

    public void Dispose() { }
}
