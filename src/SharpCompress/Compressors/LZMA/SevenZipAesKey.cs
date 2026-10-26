using System.Security.Cryptography;

namespace SharpCompress.Compressors.LZMA;

/// <summary>
/// 7zAES key derivation, shared by <see cref="AesDecoderStream" /> and the 7z writer.
/// </summary>
internal static class SevenZipAesKey
{
    /// <summary>
    /// 7-Zip's default for new archives: 2^19 SHA-256 rounds.
    /// </summary>
    public const int DefaultNumCyclesPower = 19;

    public static byte[]? Derive(int numCyclesPower, byte[] salt, byte[] pass)
    {
        if (numCyclesPower == 0x3F)
        {
            var key = new byte[32];

            int pos;
            for (pos = 0; pos < salt.Length; pos++)
            {
                key[pos] = salt[pos];
            }

            for (var i = 0; i < pass.Length && pos < 32; i++)
            {
                key[pos++] = pass[i];
            }

            return key;
        }
        else
        {
            using var sha = SHA256.Create();
            var counter = new byte[8];
            var numRounds = 1L << numCyclesPower;
            for (long round = 0; round < numRounds; round++)
            {
                sha.TransformBlock(salt, 0, salt.Length, null, 0);
                sha.TransformBlock(pass, 0, pass.Length, null, 0);
                sha.TransformBlock(counter, 0, 8, null, 0);

                // This mirrors the counter so we don't have to convert long to byte[] each round.
                // (It also ensures the counter is little endian, which BitConverter does not.)
                for (var i = 0; i < 8; i++)
                {
                    if (++counter[i] != 0)
                    {
                        break;
                    }
                }
            }

            sha.TransformFinalBlock(counter, 0, 0);
            return sha.Hash;
        }
    }
}
