using System;
using System.Threading;
using System.Threading.Tasks;
using SharpCompress.Common.Rar;
using SharpCompress.IO;

namespace SharpCompress.Common.Rar.Headers;

// http://www.forensicswiki.org/w/images/5/5b/RARFileStructure.txt
// https://www.rarlab.com/technote.htm
internal class RarHeader : IRarHeader
{
    private HeaderType _headerType;
    private bool _isRar5;

    protected RarHeader()
    {
        ArchiveEncoding = new ArchiveEncoding();
    }

    internal static RarHeader? TryReadBase(
        RarCrcBinaryReader reader,
        bool isRar5,
        IArchiveEncoding archiveEncoding
    )
    {
        try
        {
            var header = new RarHeader();
            header.Initialize(reader, isRar5, archiveEncoding);
            return header;
        }
        catch (InvalidFormatException)
        {
            return null;
        }
    }

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

    private void Initialize(
        RarCrcBinaryReader reader,
        bool isRar5,
        IArchiveEncoding archiveEncoding
    )
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
                await reader.ReadRarVIntUInt32Async( 3, cancellationToken).ConfigureAwait(false);
            reader.Mark();
            HeaderCode = await reader.ReadRarVIntByteAsync(cancellationToken: cancellationToken).ConfigureAwait(false);
            HeaderFlags = await reader
                .ReadRarVIntUInt16Async(2,cancellationToken)
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
                    await reader.ReadRarVIntAsync(cancellationToken: cancellationToken).ConfigureAwait(false);
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

    internal static T CreateChild<T>(
        RarHeader header,
        RarCrcBinaryReader reader,
        HeaderType headerType
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
        child.ReadFinish(reader);

        var n = child.RemainingHeaderBytes(reader);
        if (n > 0)
        {
            reader.ReadBytes(n);
        }

        child.VerifyHeaderCrc(reader.GetCrc32());
        return child;
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

    protected int RemainingHeaderBytes(MarkingBinaryReader reader) =>
        checked(HeaderSize - (int)reader.CurrentReadByteCount);

    protected int RemainingHeaderBytesAsync(AsyncMarkingBinaryReader reader) =>
        checked(HeaderSize - (int)reader.CurrentReadByteCount);

    protected virtual void ReadFinish(MarkingBinaryReader reader) =>
        throw new NotImplementedException();

    protected virtual ValueTask ReadFinishAsync(
        AsyncMarkingBinaryReader reader,
        CancellationToken cancellationToken = default
    ) => throw new NotImplementedException();

    private void VerifyHeaderCrc(uint crc32)
    {
        var b = (IsRar5 ? crc32 : (ushort)crc32) == HeaderCrc;
        if (!b)
        {
            throw new InvalidFormatException("rar header crc mismatch");
        }
    }

    public HeaderType HeaderType => _headerType;

    internal bool IsRar5 => _isRar5;

    protected uint HeaderCrc { get; private set; }

    internal byte HeaderCode { get; private set; }

    protected ushort HeaderFlags { get; private set; }

    protected bool HasHeaderFlag(ushort flag) => (HeaderFlags & flag) == flag;

    protected int HeaderSize { get; private set; }

    internal IArchiveEncoding ArchiveEncoding { get; private set; }

    /// <summary>
    /// Extra header size.
    /// </summary>
    protected uint ExtraSize { get; private set; }

    /// <summary>
    /// Size of additional data (eg file contents)
    /// </summary>
    protected long AdditionalDataSize { get; private set; }
}
