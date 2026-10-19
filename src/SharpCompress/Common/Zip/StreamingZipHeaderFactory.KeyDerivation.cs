using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using SharpCompress.Common.Zip.Headers;

namespace SharpCompress.Common.Zip;

/// <summary>
/// WinZip AES derives the keys of every entry with PBKDF2 (1000 iterations of HMAC-SHA1), which
/// dominates the cost of reading archives with many small entries. With parallelism enabled, the
/// keys of the entries in the central directory lookahead are derived on the thread pool while the
/// earlier entries are being read.
/// </summary>
internal sealed partial class StreamingZipHeaderFactory
{
    private const int LocalHeaderNameLengthOffset = 26;

    private readonly bool _deriveKeysAhead;
    private readonly Dictionary<string, DerivedKey> _derivedKeys = new(StringComparer.Ordinal);

    private sealed class DerivedKey
    {
        public DerivedKey(WinzipAesKeySize keySize, byte[] salt, Task<byte[]> key)
        {
            KeySize = keySize;
            Salt = salt;
            Key = key;
        }

        public WinzipAesKeySize KeySize { get; }

        public byte[] Salt { get; }

        public Task<byte[]> Key { get; }
    }

    private void StartKeyDerivation(Stream stream, DirectoryEntryHeader directoryEntry)
    {
        if (!TryGetKeyDerivationInput(directoryEntry, out var password, out var keySize))
        {
            return;
        }

        try
        {
            // The salt follows the local header; the caller restores the stream position.
            var lengths = new byte[4];
            stream.Position =
                directoryEntry.RelativeOffsetOfEntryHeader + LocalHeaderNameLengthOffset;
            stream.ReadExact(lengths, 0, lengths.Length);
            stream.Position += GetNameAndExtraLength(lengths);
            var salt = new byte[WinzipAesEncryptionData.KeyLengthInBytes(keySize) / 2];
            stream.ReadExact(salt, 0, salt.Length);
            AddDerivedKey(directoryEntry.Name!, keySize, salt, password);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // The key is derived when the entry is read instead.
        }
    }

    private async ValueTask StartKeyDerivationAsync(
        Stream stream,
        DirectoryEntryHeader directoryEntry
    )
    {
        if (!TryGetKeyDerivationInput(directoryEntry, out var password, out var keySize))
        {
            return;
        }

        try
        {
            // The salt follows the local header; the caller restores the stream position.
            var lengths = new byte[4];
            stream.Position =
                directoryEntry.RelativeOffsetOfEntryHeader + LocalHeaderNameLengthOffset;
            await stream.ReadExactAsync(lengths, 0, lengths.Length).ConfigureAwait(false);
            stream.Position += GetNameAndExtraLength(lengths);
            var salt = new byte[WinzipAesEncryptionData.KeyLengthInBytes(keySize) / 2];
            await stream.ReadExactAsync(salt, 0, salt.Length).ConfigureAwait(false);
            AddDerivedKey(directoryEntry.Name!, keySize, salt, password);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // The key is derived when the entry is read instead.
        }
    }

    private bool TryGetKeyDerivationInput(
        DirectoryEntryHeader directoryEntry,
        out string password,
        out WinzipAesKeySize keySize
    )
    {
        password = Password!;
        keySize = default;
        if (
            !_deriveKeysAhead
            || Password is null
            || directoryEntry.Name is null
            || directoryEntry.CompressionMethod != ZipCompressionMethod.WinzipAes
            || !FlagUtility.HasFlag(directoryEntry.Flags, HeaderFlags.Encrypted)
        )
        {
            return false;
        }

        var aesExtra = directoryEntry.Extra.FirstOrDefault(x => x.Type == ExtraDataType.WinZipAes);
        if (aesExtra is null || aesExtra.DataBytes.Length < 5)
        {
            return false;
        }

        // An invalid strength is reported when the entry itself is read.
        keySize = (WinzipAesKeySize)aesExtra.DataBytes[4];
        return keySize
            is WinzipAesKeySize.KeySize128
                or WinzipAesKeySize.KeySize192
                or WinzipAesKeySize.KeySize256;
    }

    private static int GetNameAndExtraLength(byte[] lengths) =>
        BinaryPrimitives.ReadUInt16LittleEndian(lengths)
        + BinaryPrimitives.ReadUInt16LittleEndian(lengths.AsSpan(2));

    private void AddDerivedKey(string name, WinzipAesKeySize keySize, byte[] salt, string password)
    {
        if (_derivedKeys.Count >= _lookaheadDepth * 4)
        {
            // Keys of entries read out of central directory order are never claimed.
            _derivedKeys.Clear();
        }

        _derivedKeys[name] = new DerivedKey(
            keySize,
            salt,
            Task.Run(() => WinzipAesEncryptionData.DeriveKey(keySize, salt, password))
        );
    }

    /// <summary>
    /// Claims the key derived ahead of time for the entry, provided it was derived from the same salt.
    /// A failed derivation is ignored, so the key is derived (and errors reported) inline.
    /// </summary>
    private Task<byte[]>? TakeDerivedKey(
        ZipFileEntry entryHeader,
        WinzipAesKeySize keySize,
        byte[] salt
    )
    {
        if (
            entryHeader.Name is null
            || !_derivedKeys.TryGetValue(entryHeader.Name, out var derived)
        )
        {
            return null;
        }

        _derivedKeys.Remove(entryHeader.Name);
        if (
            derived.KeySize != keySize
            || !derived.Salt.AsSpan().SequenceEqual(salt)
            || derived.Key.IsFaulted
            || derived.Key.IsCanceled
        )
        {
            return null;
        }

        return derived.Key;
    }

    protected override WinzipAesEncryptionData CreateWinzipAesEncryptionData(
        ZipFileEntry entryHeader,
        WinzipAesKeySize keySize,
        byte[] salt,
        byte[] passwordVerifyValue,
        string password
    )
    {
        var derivedKey = TakeDerivedKey(entryHeader, keySize, salt);
        if (derivedKey is null)
        {
            return base.CreateWinzipAesEncryptionData(
                entryHeader,
                keySize,
                salt,
                passwordVerifyValue,
                password
            );
        }

        // Synchronous API: the derivation is CPU-bound and already running on the thread pool.
#pragma warning disable VSTHRD002 // Avoid problematic synchronous waits
        return new WinzipAesEncryptionData(
            keySize,
            derivedKey.GetAwaiter().GetResult(),
            passwordVerifyValue
        );
#pragma warning restore VSTHRD002
    }

    protected override async ValueTask<WinzipAesEncryptionData> CreateWinzipAesEncryptionDataAsync(
        ZipFileEntry entryHeader,
        WinzipAesKeySize keySize,
        byte[] salt,
        byte[] passwordVerifyValue,
        string password
    )
    {
        var derivedKey = TakeDerivedKey(entryHeader, keySize, salt);
        if (derivedKey is null)
        {
            return base.CreateWinzipAesEncryptionData(
                entryHeader,
                keySize,
                salt,
                passwordVerifyValue,
                password
            );
        }

        return new WinzipAesEncryptionData(
            keySize,
            await derivedKey.ConfigureAwait(false),
            passwordVerifyValue
        );
    }
}
