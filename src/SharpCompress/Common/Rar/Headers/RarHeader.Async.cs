using System;
using System.Threading;
using System.Threading.Tasks;
using SharpCompress.Common.Rar;
using SharpCompress.IO;

namespace SharpCompress.Common.Rar.Headers;

internal partial class RarHeader
{
    internal static async ValueTask<RarHeader?> TryReadBaseAsync(
        AsyncRarCrcBinaryReader reader,
        bool isRar5,
        IArchiveEncoding archiveEncoding,
        CancellationToken cancellationToken = default
    )
    {
        try
        {
            var header = new RarHeader();
            await header
                .InitializeAsync(reader, isRar5, archiveEncoding, cancellationToken)
                .ConfigureAwait(false);
            return header;
        }
        catch (InvalidFormatException)
        {
            return null;
        }
    }

    private async ValueTask InitializeAsync(
        AsyncRarCrcBinaryReader reader,
        bool isRar5,
        IArchiveEncoding archiveEncoding,
        CancellationToken cancellationToken
    )
    {
        _headerType = HeaderType.Null;
        _isRar5 = isRar5;
        ArchiveEncoding = archiveEncoding;
        if (IsRar5)
        {
            HeaderCrc = await reader.ReadUInt32Async(cancellationToken).ConfigureAwait(false);
            reader.ResetCrc();
            HeaderSize = (int)
                await reader.ReadRarVIntUInt32Async(3, cancellationToken).ConfigureAwait(false);
            reader.Mark();
            HeaderCode = await reader
                .ReadRarVIntByteAsync(cancellationToken: cancellationToken)
                .ConfigureAwait(false);
            HeaderFlags = await reader
                .ReadRarVIntUInt16Async(2, cancellationToken)
                .ConfigureAwait(false);

            if (HasHeaderFlag(HeaderFlagsV5.HAS_EXTRA))
            {
                ExtraSize = await reader
                    .ReadRarVIntUInt32Async(cancellationToken: cancellationToken)
                    .ConfigureAwait(false);
            }
            if (HasHeaderFlag(HeaderFlagsV5.HAS_DATA))
            {
                AdditionalDataSize = (long)
                    await reader
                        .ReadRarVIntAsync(cancellationToken: cancellationToken)
                        .ConfigureAwait(false);
            }
        }
        else
        {
            reader.Mark();
            HeaderCrc = await reader.ReadUInt16Async(cancellationToken).ConfigureAwait(false);
            reader.ResetCrc();
            HeaderCode = await reader.ReadByteAsync(cancellationToken).ConfigureAwait(false);
            HeaderFlags = await reader.ReadUInt16Async(cancellationToken).ConfigureAwait(false);
            HeaderSize = await reader.ReadInt16Async(cancellationToken).ConfigureAwait(false);
            if (HasHeaderFlag(HeaderFlagsV4.HAS_DATA))
            {
                AdditionalDataSize = await reader
                    .ReadUInt32Async(cancellationToken)
                    .ConfigureAwait(false);
            }
        }
    }

    internal static async ValueTask<T> CreateChildAsync<T>(
        RarHeader header,
        AsyncRarCrcBinaryReader reader,
        HeaderType headerType,
        CancellationToken cancellationToken = default
    )
        where T : RarHeader, new()
    {
        var child = new T() { ArchiveEncoding = header.ArchiveEncoding };
        child._headerType = headerType;
        child._isRar5 = header.IsRar5;
        child.HeaderCrc = header.HeaderCrc;
        child.HeaderCode = header.HeaderCode;
        child.HeaderFlags = header.HeaderFlags;
        child.HeaderSize = header.HeaderSize;
        child.ExtraSize = header.ExtraSize;
        child.AdditionalDataSize = header.AdditionalDataSize;
        await child.ReadFinishAsync(reader, cancellationToken).ConfigureAwait(false);

        var n = child.RemainingHeaderBytesAsync(reader);
        if (n > 0)
        {
            await reader.ReadBytesAsync(n, cancellationToken).ConfigureAwait(false);
        }

        child.VerifyHeaderCrc(reader.GetCrc32());
        return child;
    }
}
