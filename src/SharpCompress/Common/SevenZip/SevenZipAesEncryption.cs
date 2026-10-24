using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using SharpCompress.Compressors.LZMA;

namespace SharpCompress.Common.SevenZip;

/// <summary>
/// 7zAES settings for one archive being written. The key is derived once, without a salt, and
/// every folder gets its own random IV, which is what 7-Zip does by default.
/// </summary>
internal sealed class SevenZipAesEncryption
{
    private const int IvSize = 16;

    private readonly byte[] _key;
    private readonly int _numCyclesPower;

    private SevenZipAesEncryption(byte[] key, int numCyclesPower)
    {
        _key = key;
        _numCyclesPower = numCyclesPower;
    }

    public static SevenZipAesEncryption Create(
        string password,
        int numCyclesPower = SevenZipAesKey.DefaultNumCyclesPower
    )
    {
        // 7-Zip hashes the UTF-16LE password, as AesDecoderStream expects.
        var key =
            SevenZipAesKey.Derive(numCyclesPower, [], Encoding.Unicode.GetBytes(password))
            ?? throw new ArchiveOperationException("7z AES key derivation failed.");
        return new SevenZipAesEncryption(key, numCyclesPower);
    }

    /// <summary>
    /// Creates an encoder for one folder and returns the AES coder properties to store with it.
    /// </summary>
    public AesEncoderStream CreateEncoder(Stream output, out byte[] coderProperties)
    {
        var iv = new byte[IvSize];
        using (var random = RandomNumberGenerator.Create())
        {
            random.GetBytes(iv);
        }

        // Byte 0: 0x40 = IV present, low six bits = NumCyclesPower; no salt bit.
        // Byte 1: high nibble = salt size beyond the salt bit (0), low nibble = IV size beyond
        // the IV bit (15), giving a 16-byte IV. See AesDecoderStream.Init.
        coderProperties = new byte[2 + IvSize];
        coderProperties[0] = (byte)(0x40 | _numCyclesPower);
        coderProperties[1] = IvSize - 1;
        Buffer.BlockCopy(iv, 0, coderProperties, 2, IvSize);
        return new AesEncoderStream(output, _key, iv);
    }
}
