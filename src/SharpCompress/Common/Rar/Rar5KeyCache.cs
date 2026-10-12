using System.Collections.Generic;
using System.Linq;
using SharpCompress.Common.Rar.Headers;

namespace SharpCompress.Common.Rar;

// Cache only KDF output, never entry-specific IVs, checks, or AES transforms.
internal sealed class Rar5KeyCache
{
    private const int Capacity = 4;
    private readonly List<(string Password, byte[] Salt, int Lg2Count, List<byte[]> Keys)> entries =
        new();

    internal List<byte[]> GetKeys(string password, byte[] salt, int lg2Count)
    {
        // Metadata can be mutated after parsing; validate before C# masks the shift count.
        if (lg2Count < 0 || lg2Count > EncryptionConstV5.CRYPT5_KDF_LG2_COUNT_MAX)
        {
            throw new CryptographicException($"Unsupported LG2 count of {lg2Count}.");
        }

        lock (entries)
        {
            foreach (var (cachedPassword, cachedSalt, cachedCount, cachedKeys) in entries)
            {
                if (
                    cachedPassword == password
                    && cachedCount == lg2Count
                    && cachedSalt.SequenceEqual(salt)
                )
                {
                    return cachedKeys;
                }
            }

            // Snapshot mutable metadata so later changes cannot corrupt cache identity.
            var snapshot = (byte[])salt.Clone();
            var keys = CryptKey5.GenerateRarPBKDF2Key(password, snapshot, 1 << lg2Count);
            if (entries.Count == Capacity)
            {
                entries.RemoveAt(0);
            }
            entries.Add((password, snapshot, lg2Count, keys));
            return keys;
        }
    }
}
