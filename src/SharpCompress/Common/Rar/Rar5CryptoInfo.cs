using System;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using SharpCompress.Common.Rar.Headers;
using SharpCompress.IO;

namespace SharpCompress.Common.Rar;

internal class Rar5CryptoInfo
{
    private Rar5CryptoInfo() { }

    public static Rar5CryptoInfo Create(MarkingBinaryReader reader, bool readInitV)
    {
        var cryptoInfo = new Rar5CryptoInfo();
        var cryptVersion = reader.ReadRarVIntUInt32();
        if (cryptVersion > EncryptionConstV5.VERSION)
        {
            throw new CryptographicException($"Unsupported crypto version of {cryptVersion}");
        }
        var encryptionFlags = reader.ReadRarVIntUInt32();
        cryptoInfo.UsePswCheck = FlagUtility.HasFlag(
            encryptionFlags,
            EncryptionFlagsV5.CHFL_CRYPT_PSWCHECK
        );
        cryptoInfo.LG2Count = reader.ReadRarVIntByte(1);

        if (cryptoInfo.LG2Count > EncryptionConstV5.CRYPT5_KDF_LG2_COUNT_MAX)
        {
            throw new CryptographicException($"Unsupported LG2 count of {cryptoInfo.LG2Count}.");
        }

        cryptoInfo.Salt = reader.ReadBytes(EncryptionConstV5.SIZE_SALT50);

        if (readInitV) // File header needs to read IV here
        {
            cryptoInfo.ReadInitV(reader);
        }

        if (cryptoInfo.UsePswCheck)
        {
            cryptoInfo.PswCheck = reader.ReadBytes(EncryptionConstV5.SIZE_PSWCHECK);
            var _pswCheckCsm = reader.ReadBytes(EncryptionConstV5.SIZE_PSWCHECK_CSUM);

#if LEGACY_DOTNET
            var sha = SHA256.Create();
            cryptoInfo.UsePswCheck = sha.ComputeHash(cryptoInfo.PswCheck)
                .AsSpan()
                .StartsWith(_pswCheckCsm.AsSpan());
#else
            cryptoInfo.UsePswCheck = SHA256
                .HashData(cryptoInfo.PswCheck)
                .AsSpan()
                .StartsWith(_pswCheckCsm.AsSpan());
#endif
        }
        return cryptoInfo;
    }

    public static async ValueTask<Rar5CryptoInfo> CreateAsync(
        AsyncMarkingBinaryReader reader,
        bool readInitV
    )
    {
        var cryptoInfo = new Rar5CryptoInfo();
        var cryptVersion = await reader
            .ReadRarVIntUInt32Async(cancellationToken: CancellationToken.None)
            .ConfigureAwait(false);
        if (cryptVersion > EncryptionConstV5.VERSION)
        {
            throw new CryptographicException($"Unsupported crypto version of {cryptVersion}");
        }
        var encryptionFlags = await reader
            .ReadRarVIntUInt32Async(cancellationToken: CancellationToken.None)
            .ConfigureAwait(false);
        cryptoInfo.UsePswCheck = FlagUtility.HasFlag(
            encryptionFlags,
            EncryptionFlagsV5.CHFL_CRYPT_PSWCHECK
        );
        cryptoInfo.LG2Count = (int)
            await reader
                .ReadRarVIntUInt32Async(cancellationToken: CancellationToken.None)
                .ConfigureAwait(false);
        if (cryptoInfo.LG2Count > EncryptionConstV5.CRYPT5_KDF_LG2_COUNT_MAX)
        {
            throw new CryptographicException($"Unsupported LG2 count of {cryptoInfo.LG2Count}.");
        }

        cryptoInfo.Salt = await reader
            .ReadBytesAsync(EncryptionConstV5.SIZE_SALT50, CancellationToken.None)
            .ConfigureAwait(false);

        if (readInitV)
        {
            await cryptoInfo.ReadInitVAsync(reader).ConfigureAwait(false);
        }

        if (cryptoInfo.UsePswCheck)
        {
            cryptoInfo.PswCheck = await reader
                .ReadBytesAsync(EncryptionConstV5.SIZE_PSWCHECK, CancellationToken.None)
                .ConfigureAwait(false);
            var _pswCheckCsm = await reader
                .ReadBytesAsync(EncryptionConstV5.SIZE_PSWCHECK_CSUM, CancellationToken.None)
                .ConfigureAwait(false);

#if LEGACY_DOTNET
            var sha = SHA256.Create();
            cryptoInfo.UsePswCheck = sha.ComputeHash(cryptoInfo.PswCheck)
                .AsSpan()
                .StartsWith(_pswCheckCsm.AsSpan());
#else
            cryptoInfo.UsePswCheck = SHA256
                .HashData(cryptoInfo.PswCheck)
                .AsSpan()
                .StartsWith(_pswCheckCsm.AsSpan());
#endif
        }
        return cryptoInfo;
    }

    public void ReadInitV(MarkingBinaryReader reader) =>
        InitV = reader.ReadBytes(EncryptionConstV5.SIZE_INITV);

    public async ValueTask ReadInitVAsync(AsyncMarkingBinaryReader reader) =>
        InitV = await reader
            .ReadBytesAsync(EncryptionConstV5.SIZE_INITV, CancellationToken.None)
            .ConfigureAwait(false);

    public bool UsePswCheck = false;

    public int LG2Count = 0;

    public byte[] InitV = [];

    public byte[] Salt = [];

    public byte[] PswCheck = [];
}
