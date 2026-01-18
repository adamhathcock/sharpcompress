using System;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using SharpCompress.Common.Rar.Headers;
using SharpCompress.IO;

namespace SharpCompress.Common.Rar;

internal class Rar5CryptoInfo
{
    public Rar5CryptoInfo() { }

    public static async ValueTask<Rar5CryptoInfo> CreateAsync(
        AsyncMarkingBinaryReader reader,
        bool readInitV,
        CancellationToken cancellationToken = default
    )
    {
        var info = new Rar5CryptoInfo();

        var cryptVersion = await reader.ReadRarVIntUInt32Async().ConfigureAwait(false);
        if (cryptVersion > EncryptionConstV5.VERSION)
        {
            throw new CryptographicException($"Unsupported crypto version of {cryptVersion}");
        }
        var encryptionFlags = await reader.ReadRarVIntUInt32Async().ConfigureAwait(false);
        info.UsePswCheck = FlagUtility.HasFlag(
            encryptionFlags,
            EncryptionFlagsV5.CHFL_CRYPT_PSWCHECK
        );
        info.LG2Count = await reader.ReadRarVIntByteAsync(1).ConfigureAwait(false);

        if (info.LG2Count > EncryptionConstV5.CRYPT5_KDF_LG2_COUNT_MAX)
        {
            throw new CryptographicException($"Unsupported LG2 count of {info.LG2Count}.");
        }

        info.Salt = await reader
            .ReadBytesAsync(EncryptionConstV5.SIZE_SALT50, cancellationToken)
            .ConfigureAwait(false);

        if (readInitV)
        {
            await info.ReadInitVAsync(reader, cancellationToken).ConfigureAwait(false);
        }

        if (info.UsePswCheck)
        {
            info.PswCheck = await reader
                .ReadBytesAsync(EncryptionConstV5.SIZE_PSWCHECK, cancellationToken)
                .ConfigureAwait(false);
            var _pswCheckCsm = await reader
                .ReadBytesAsync(EncryptionConstV5.SIZE_PSWCHECK_CSUM, cancellationToken)
                .ConfigureAwait(false);

            var sha = SHA256.Create();
            info.UsePswCheck = sha.ComputeHash(info.PswCheck)
                .AsSpan()
                .StartsWith(_pswCheckCsm.AsSpan());
        }

        return info;
    }

    public Rar5CryptoInfo(MarkingBinaryReader reader, bool readInitV)
    {
        var cryptVersion = reader.ReadRarVIntUInt32();
        if (cryptVersion > EncryptionConstV5.VERSION)
        {
            throw new CryptographicException($"Unsupported crypto version of {cryptVersion}");
        }
        var encryptionFlags = reader.ReadRarVIntUInt32();
        UsePswCheck = FlagUtility.HasFlag(encryptionFlags, EncryptionFlagsV5.CHFL_CRYPT_PSWCHECK);
        LG2Count = reader.ReadRarVIntByte(1);

        if (LG2Count > EncryptionConstV5.CRYPT5_KDF_LG2_COUNT_MAX)
        {
            throw new CryptographicException($"Unsupported LG2 count of {LG2Count}.");
        }

        Salt = reader.ReadBytes(EncryptionConstV5.SIZE_SALT50);

        if (readInitV) // File header needs to read IV here
        {
            ReadInitV(reader);
        }

        if (UsePswCheck)
        {
            PswCheck = reader.ReadBytes(EncryptionConstV5.SIZE_PSWCHECK);
            var _pswCheckCsm = reader.ReadBytes(EncryptionConstV5.SIZE_PSWCHECK_CSUM);

            var sha = SHA256.Create();
            UsePswCheck = sha.ComputeHash(PswCheck).AsSpan().StartsWith(_pswCheckCsm.AsSpan());
        }
    }

    public void ReadInitV(MarkingBinaryReader reader) =>
        InitV = reader.ReadBytes(EncryptionConstV5.SIZE_INITV);

    public async ValueTask ReadInitVAsync(
        AsyncMarkingBinaryReader reader,
        CancellationToken cancellationToken = default
    ) =>
        InitV = await reader
            .ReadBytesAsync(EncryptionConstV5.SIZE_INITV, cancellationToken)
            .ConfigureAwait(false);

    public bool UsePswCheck = false;

    public int LG2Count = 0;

    public byte[] InitV = { };

    public byte[] Salt = { };

    public byte[] PswCheck = { };
}
