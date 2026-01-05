using System;
using System.Threading;
using System.Threading.Tasks;
using SharpCompress.IO;

namespace SharpCompress.Common.Rar.Headers;

// http://www.forensicswiki.org/w/images/5/5b/RARFileStructure.txt
// https://www.rarlab.com/technote.htm
internal class RarHeader : IRarHeader
{
    private readonly HeaderType _headerType;
    private bool _isRar5;

    internal static RarHeader? TryReadBase(
        RarCrcBinaryReader reader,
        bool isRar5,
        ArchiveEncoding archiveEncoding
    )
    {
        try
        {
            return new RarHeader(reader, isRar5, archiveEncoding);
        }
        catch (InvalidFormatException)
        {
            return null;
        }
    }

    internal static async Task<RarHeader?> TryReadBaseAsync(
        RarCrcBinaryReader reader,
        bool isRar5,
        ArchiveEncoding archiveEncoding,
        CancellationToken cancellationToken = default
    )
    {
        try
        {
            return await CreateAsync(reader, isRar5, archiveEncoding, cancellationToken)
                .ConfigureAwait(false);
        }
        catch (InvalidFormatException)
        {
            return null;
        }
    }

    private static async Task<RarHeader> CreateAsync(
        RarCrcBinaryReader reader,
        bool isRar5,
        ArchiveEncoding archiveEncoding,
        CancellationToken cancellationToken
    )
    {
        var header = new RarHeader();
        await header
            .InitializeAsync(reader, isRar5, archiveEncoding, cancellationToken)
            .ConfigureAwait(false);
        return header;
    }

    private RarHeader()
    {
        _headerType = HeaderType.Null;
        ArchiveEncoding = new ArchiveEncoding();
    }

    private async Task InitializeAsync(
        RarCrcBinaryReader reader,
        bool isRar5,
        ArchiveEncoding archiveEncoding,
        CancellationToken cancellationToken
    )
    {
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
                .ReadRarVIntByteAsync(2, cancellationToken)
                .ConfigureAwait(false);
            HeaderFlags = await reader
                .ReadRarVIntUInt16Async(2, cancellationToken)
                .ConfigureAwait(false);

            if (HasHeaderFlag(HeaderFlagsV5.HAS_EXTRA))
            {
                ExtraSize = await reader
                    .ReadRarVIntUInt32Async(5, cancellationToken)
                    .ConfigureAwait(false);
            }
            if (HasHeaderFlag(HeaderFlagsV5.HAS_DATA))
            {
                AdditionalDataSize = (long)
                    await reader.ReadRarVIntAsync(10, cancellationToken).ConfigureAwait(false);
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

    private RarHeader(RarCrcBinaryReader reader, bool isRar5, ArchiveEncoding archiveEncoding)
    {
        _headerType = HeaderType.Null;
        _isRar5 = isRar5;
        ArchiveEncoding = archiveEncoding;
        if (IsRar5)
        {
            HeaderCrc = reader.ReadUInt32();
            reader.ResetCrc();
            HeaderSize = (int)reader.ReadRarVIntUInt32(3);
            reader.Mark();
            HeaderCode = reader.ReadRarVIntByte();
            HeaderFlags = reader.ReadRarVIntUInt16(2);

            if (HasHeaderFlag(HeaderFlagsV5.HAS_EXTRA))
            {
                ExtraSize = reader.ReadRarVIntUInt32();
            }
            if (HasHeaderFlag(HeaderFlagsV5.HAS_DATA))
            {
                AdditionalDataSize = (long)reader.ReadRarVInt();
            }
        }
        else
        {
            reader.Mark();
            HeaderCrc = reader.ReadUInt16();
            reader.ResetCrc();
            HeaderCode = reader.ReadByte();
            HeaderFlags = reader.ReadUInt16();
            HeaderSize = reader.ReadInt16();
            if (HasHeaderFlag(HeaderFlagsV4.HAS_DATA))
            {
                AdditionalDataSize = reader.ReadUInt32();
            }
        }
    }

    protected RarHeader(RarHeader header, RarCrcBinaryReader reader, HeaderType headerType)
    {
        _headerType = headerType;
        _isRar5 = header.IsRar5;
        HeaderCrc = header.HeaderCrc;
        HeaderCode = header.HeaderCode;
        HeaderFlags = header.HeaderFlags;
        HeaderSize = header.HeaderSize;
        ExtraSize = header.ExtraSize;
        AdditionalDataSize = header.AdditionalDataSize;
        ArchiveEncoding = header.ArchiveEncoding;
        ReadFinish(reader);

        var n = RemainingHeaderBytes(reader);
        if (n > 0)
        {
            reader.ReadBytes(n);
        }

        VerifyHeaderCrc(reader.GetCrc32());
    }

    protected int RemainingHeaderBytes(MarkingBinaryReader reader) =>
        checked(HeaderSize - (int)reader.CurrentReadByteCount);

    protected virtual void ReadFinish(MarkingBinaryReader reader) =>
        throw new NotImplementedException();

    private void VerifyHeaderCrc(uint crc32)
    {
        var b = (IsRar5 ? crc32 : (ushort)crc32) == HeaderCrc;
        if (!b)
        {
            throw new InvalidFormatException("rar header crc mismatch");
        }
    }

    public HeaderType HeaderType => _headerType;

    protected bool IsRar5 => _isRar5;

    protected uint HeaderCrc { get; private set; }

    internal byte HeaderCode { get; private set; }

    protected ushort HeaderFlags { get; private set; }

    protected bool HasHeaderFlag(ushort flag) => (HeaderFlags & flag) == flag;

    protected int HeaderSize { get; private set; }

    internal ArchiveEncoding ArchiveEncoding { get; private set; }

    /// <summary>
    /// Extra header size.
    /// </summary>
    protected uint ExtraSize { get; private set; }

    /// <summary>
    /// Size of additional data (eg file contents)
    /// </summary>
    protected long AdditionalDataSize { get; private set; }
}
