using System.Collections.Generic;
using System.Linq;

namespace SharpCompress.Common.Rar;

// Cache only KDF output, never entry-specific IVs, checks, or AES transforms.
internal sealed class Rar5KeyCache
{
    private const int Capacity = 4;
    private readonly List<(string Password, byte[] Salt, int Lg2Count, List<byte[]> Keys)> entries =
        new();

    internal List<byte[]> GetKeys(string password, byte[] salt, int lg2Count)
    {
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
