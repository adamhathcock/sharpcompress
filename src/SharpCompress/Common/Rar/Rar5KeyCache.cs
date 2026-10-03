using System;
using System.Linq;

namespace SharpCompress.Common.Rar;

/// <summary>
/// Shares RAR5 PBKDF2 output between the encrypted headers and files of a volume, which RAR
/// encrypts with the same salt. Without it every encrypted file repeats the full key derivation.
/// unrar keeps an equivalent cache (KDF5Cache) for the same reason.
/// </summary>
internal sealed class Rar5KeyCache
{
    // Same size as unrar's KDF5Cache; more than one entry is only used when the password or
    // salt changes.
    private const int CAPACITY = 4;

    private readonly Entry?[] _entries = new Entry?[CAPACITY];
    private int _nextSlot;

    // Lets tests observe cache hits.
    public int DerivationCount { get; private set; }

    public Rar5DerivedKey GetOrDerive(
        string password,
        byte[] salt,
        int lg2Count,
        Func<string, byte[], int, Rar5DerivedKey> derive
    )
    {
        lock (_entries)
        {
            foreach (var entry in _entries)
            {
                if (
                    entry is not null
                    && entry.Lg2Count == lg2Count
                    && entry.Password == password
                    && entry.Salt.SequenceEqual(salt)
                )
                {
                    return entry.Key;
                }
            }

            var key = derive(password, salt, lg2Count);
            DerivationCount++;
            // Snapshot the salt so in-place changes cannot reuse an unrelated key.
            _entries[_nextSlot] = new Entry(password, (byte[])salt.Clone(), lg2Count, key);
            _nextSlot = (_nextSlot + 1) % CAPACITY;
            return key;
        }
    }

    private sealed class Entry(string password, byte[] salt, int lg2Count, Rar5DerivedKey key)
    {
        public string Password { get; } = password;

        public byte[] Salt { get; } = salt;

        public int Lg2Count { get; } = lg2Count;

        public Rar5DerivedKey Key { get; } = key;
    }
}
