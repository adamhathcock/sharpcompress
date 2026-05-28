using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using SharpCompress.Common.Rar;
using SharpCompress.IO;
using size_t = System.UInt32;

namespace SharpCompress.Common.Rar.Headers;

internal partial class FileHeader
{
    public static async ValueTask<FileHeader> CreateAsync(
        RarHeader header,
        AsyncRarCrcBinaryReader reader,
        HeaderType headerType,
        CancellationToken cancellationToken = default
    ) =>
        await CreateChildAsync<FileHeader>(header, reader, headerType, cancellationToken)
            .ConfigureAwait(false);

    protected override async ValueTask ReadFinishAsync(
        AsyncMarkingBinaryReader reader,
        CancellationToken cancellationToken
    )
    {
        if (IsRar5)
        {
            await ReadFromReaderV5Async(reader, cancellationToken).ConfigureAwait(false);
        }
        else
        {
            await ReadFromReaderV4Async(reader, cancellationToken).ConfigureAwait(false);
        }
    }

    private async ValueTask ReadFromReaderV5Async(
        AsyncMarkingBinaryReader reader,
        CancellationToken cancellationToken
    )
    {
        Flags = await reader
            .ReadRarVIntUInt16Async(cancellationToken: cancellationToken)
            .ConfigureAwait(false);

        var lvalue = checked(
            (long)
                await reader
                    .ReadRarVIntAsync(cancellationToken: cancellationToken)
                    .ConfigureAwait(false)
        );

        UncompressedSize = HasFlag(FileFlagsV5.UNPACKED_SIZE_UNKNOWN) ? long.MaxValue : lvalue;

        FileAttributes = await reader
            .ReadRarVIntUInt32Async(cancellationToken: cancellationToken)
            .ConfigureAwait(false);

        if (HasFlag(FileFlagsV5.HAS_MOD_TIME))
        {
            FileLastModifiedTime = Utility.UnixTimeToDateTime(
                await reader.ReadUInt32Async(cancellationToken).ConfigureAwait(false)
            );
        }

        if (HasFlag(FileFlagsV5.HAS_CRC32))
        {
            FileCrc = await reader.ReadBytesAsync(4, cancellationToken).ConfigureAwait(false);
        }

        var compressionInfo = await reader
            .ReadRarVIntUInt16Async(cancellationToken: cancellationToken)
            .ConfigureAwait(false);

        CompressionAlgorithm = (byte)((compressionInfo & 0x3f) + 50);
        IsSolid = (compressionInfo & 0x40) == 0x40;
        CompressionMethod = (byte)((compressionInfo >> 7) & 0x7);
        WindowSize = IsDirectory ? 0 : ((size_t)0x20000) << ((compressionInfo >> 10) & 0xf);

        _ = await reader
            .ReadRarVIntByteAsync(cancellationToken: cancellationToken)
            .ConfigureAwait(false);

        var nameSize = await reader
            .ReadRarVIntUInt16Async(cancellationToken: cancellationToken)
            .ConfigureAwait(false);

        var b = await reader.ReadBytesAsync(nameSize, cancellationToken).ConfigureAwait(false);
        FileName = ConvertPathV5(Encoding.UTF8.GetString(b, 0, b.Length));

        if (ExtraSize != (uint)RemainingHeaderBytesAsync(reader))
        {
            throw new InvalidFormatException("rar5 header size / extra size inconsistency");
        }

        const ushort FHEXTRA_CRYPT = 0x01;
        const ushort FHEXTRA_HASH = 0x02;
        const ushort FHEXTRA_HTIME = 0x03;
        const ushort FHEXTRA_REDIR = 0x05;

        while (reader.CurrentReadByteCount < HeaderSize)
        {
            var size = await reader
                .ReadRarVIntUInt16Async(cancellationToken: cancellationToken)
                .ConfigureAwait(false);
            var n = HeaderSize - reader.CurrentReadByteCount;
            var type = await reader
                .ReadRarVIntUInt16Async(cancellationToken: cancellationToken)
                .ConfigureAwait(false);
            switch (type)
            {
                case FHEXTRA_CRYPT:
                    {
                        Rar5CryptoInfo = await Rar5CryptoInfo
                            .CreateAsync(reader, true)
                            .ConfigureAwait(false);
                        if (Rar5CryptoInfo.PswCheck.All(singleByte => singleByte == 0))
                        {
                            Rar5CryptoInfo = null;
                        }
                    }
                    break;
                case FHEXTRA_HASH:
                    {
                        const uint FHEXTRA_HASH_BLAKE2 = 0x0;
                        const int BLAKE2_DIGEST_SIZE = 0x20;
                        if (
                            await reader
                                .ReadRarVIntUInt32Async(cancellationToken: cancellationToken)
                                .ConfigureAwait(false) == FHEXTRA_HASH_BLAKE2
                        )
                        {
                            _hash = await reader
                                .ReadBytesAsync(BLAKE2_DIGEST_SIZE, cancellationToken)
                                .ConfigureAwait(false);
                        }
                    }
                    break;
                case FHEXTRA_HTIME:
                    {
                        var flags = await reader
                            .ReadRarVIntUInt16Async(cancellationToken: cancellationToken)
                            .ConfigureAwait(false);
                        var isWindowsTime = (flags & 1) == 0;
                        if ((flags & 0x2) == 0x2)
                        {
                            FileLastModifiedTime = await ReadExtendedTimeV5Async(
                                    reader,
                                    isWindowsTime,
                                    cancellationToken
                                )
                                .ConfigureAwait(false);
                        }
                        if ((flags & 0x4) == 0x4)
                        {
                            FileCreatedTime = await ReadExtendedTimeV5Async(
                                    reader,
                                    isWindowsTime,
                                    cancellationToken
                                )
                                .ConfigureAwait(false);
                        }
                        if ((flags & 0x8) == 0x8)
                        {
                            FileLastAccessedTime = await ReadExtendedTimeV5Async(
                                    reader,
                                    isWindowsTime,
                                    cancellationToken
                                )
                                .ConfigureAwait(false);
                        }
                    }
                    break;
                case FHEXTRA_REDIR:
                    {
                        RedirType = await reader
                            .ReadRarVIntByteAsync(cancellationToken: cancellationToken)
                            .ConfigureAwait(false);
                        RedirFlags = await reader
                            .ReadRarVIntByteAsync(cancellationToken: cancellationToken)
                            .ConfigureAwait(false);
                        var nn = await reader
                            .ReadRarVIntUInt16Async(cancellationToken: cancellationToken)
                            .ConfigureAwait(false);
                        var bb = await reader
                            .ReadBytesAsync(nn, cancellationToken)
                            .ConfigureAwait(false);
                        RedirTargetName = ConvertPathV5(Encoding.UTF8.GetString(bb, 0, bb.Length));
                    }
                    break;
                default:
                    break;
            }
            var did = (int)(n - (HeaderSize - reader.CurrentReadByteCount));
            var drain = size - did;
            if (drain > 0)
            {
                await reader.ReadBytesAsync(drain, cancellationToken).ConfigureAwait(false);
            }
        }

        if (AdditionalDataSize != 0)
        {
            CompressedSize = AdditionalDataSize;
        }
    }

    private async ValueTask ReadFromReaderV4Async(
        AsyncMarkingBinaryReader reader,
        CancellationToken cancellationToken
    )
    {
        Flags = HeaderFlags;
        IsSolid = HasFlag(FileFlagsV4.SOLID);
        WindowSize = IsDirectory
            ? 0U
            : ((size_t)0x10000) << ((Flags & FileFlagsV4.WINDOW_MASK) >> 5);

        var lowUncompressedSize = await reader
            .ReadUInt32Async(cancellationToken)
            .ConfigureAwait(false);

        _ = await reader.ReadByteAsync(cancellationToken).ConfigureAwait(false);

        FileCrc = await reader.ReadBytesAsync(4, cancellationToken).ConfigureAwait(false);

        FileLastModifiedTime = Utility.DosDateToDateTime(
            await reader.ReadUInt32Async(cancellationToken).ConfigureAwait(false)
        );

        CompressionAlgorithm = await reader.ReadByteAsync(cancellationToken).ConfigureAwait(false);
        CompressionMethod = (byte)(
            (await reader.ReadByteAsync(cancellationToken).ConfigureAwait(false)) - 0x30
        );

        var nameSize = await reader.ReadInt16Async(cancellationToken).ConfigureAwait(false);

        FileAttributes = await reader.ReadUInt32Async(cancellationToken).ConfigureAwait(false);

        uint highCompressedSize = 0;
        uint highUncompressedkSize = 0;
        if (HasFlag(FileFlagsV4.LARGE))
        {
            highCompressedSize = await reader
                .ReadUInt32Async(cancellationToken)
                .ConfigureAwait(false);
            highUncompressedkSize = await reader
                .ReadUInt32Async(cancellationToken)
                .ConfigureAwait(false);
        }
        else
        {
            if (lowUncompressedSize == 0xffffffff)
            {
                lowUncompressedSize = 0xffffffff;
                highUncompressedkSize = int.MaxValue;
            }
        }
        CompressedSize = UInt32To64(highCompressedSize, checked((uint)AdditionalDataSize));
        UncompressedSize = UInt32To64(highUncompressedkSize, lowUncompressedSize);

        nameSize = nameSize > 4 * 1024 ? (short)(4 * 1024) : nameSize;

        var fileNameBytes = await reader
            .ReadBytesAsync(nameSize, cancellationToken)
            .ConfigureAwait(false);

        const int newLhdSize = 32;

        switch (HeaderCode)
        {
            case HeaderCodeV.RAR4_FILE_HEADER:
                {
                    if (HasFlag(FileFlagsV4.UNICODE))
                    {
                        var length = 0;
                        while (length < fileNameBytes.Length && fileNameBytes[length] != 0)
                        {
                            length++;
                        }
                        if (length != nameSize)
                        {
                            length++;
                            FileName = FileNameDecoder.Decode(fileNameBytes, length);
                        }
                        else
                        {
                            FileName = ArchiveEncoding.Decode(fileNameBytes);
                        }
                    }
                    else
                    {
                        FileName = ArchiveEncoding.Decode(fileNameBytes);
                    }
                    FileName = ConvertPathV4(FileName);
                }
                break;
            case HeaderCodeV.RAR4_NEW_SUB_HEADER:
                {
                    var datasize = HeaderSize - newLhdSize - nameSize;
                    if (HasFlag(FileFlagsV4.SALT))
                    {
                        datasize -= EncryptionConstV5.SIZE_SALT30;
                    }
                    if (datasize > 0)
                    {
                        SubData = await reader
                            .ReadBytesAsync(datasize, cancellationToken)
                            .ConfigureAwait(false);
                    }

                    if (NewSubHeaderType.SUBHEAD_TYPE_RR.Equals(fileNameBytes.Take(4).ToArray()))
                    {
                        if (SubData is null)
                        {
                            throw new InvalidFormatException();
                        }
                        RecoverySectors =
                            SubData[8]
                            + (SubData[9] << 8)
                            + (SubData[10] << 16)
                            + (SubData[11] << 24);
                    }
                }
                break;
        }

        if (HasFlag(FileFlagsV4.SALT))
        {
            R4Salt = await reader
                .ReadBytesAsync(EncryptionConstV5.SIZE_SALT30, cancellationToken)
                .ConfigureAwait(false);
        }
        if (HasFlag(FileFlagsV4.EXT_TIME))
        {
            if (reader.CurrentReadByteCount >= 2)
            {
                var extendedFlags = await reader
                    .ReadUInt16Async(cancellationToken)
                    .ConfigureAwait(false);
                if (FileLastModifiedTime is not null)
                {
                    FileLastModifiedTime = await ProcessExtendedTimeV4Async(
                            extendedFlags,
                            FileLastModifiedTime,
                            reader,
                            0,
                            cancellationToken
                        )
                        .ConfigureAwait(false);
                }

                FileCreatedTime = await ProcessExtendedTimeV4Async(
                        extendedFlags,
                        null,
                        reader,
                        1,
                        cancellationToken
                    )
                    .ConfigureAwait(false);
                FileLastAccessedTime = await ProcessExtendedTimeV4Async(
                        extendedFlags,
                        null,
                        reader,
                        2,
                        cancellationToken
                    )
                    .ConfigureAwait(false);
                FileArchivedTime = await ProcessExtendedTimeV4Async(
                        extendedFlags,
                        null,
                        reader,
                        3,
                        cancellationToken
                    )
                    .ConfigureAwait(false);
            }
        }
    }

    private static async ValueTask<DateTime> ReadExtendedTimeV5Async(
        AsyncMarkingBinaryReader reader,
        bool isWindowsTime,
        CancellationToken cancellationToken
    )
    {
        if (isWindowsTime)
        {
            return DateTime.FromFileTime(
                await reader.ReadInt64Async(cancellationToken).ConfigureAwait(false)
            );
        }
        else
        {
            return Utility.UnixTimeToDateTime(
                await reader.ReadUInt32Async(cancellationToken).ConfigureAwait(false)
            );
        }
    }

    private static async ValueTask<DateTime?> ProcessExtendedTimeV4Async(
        ushort extendedFlags,
        DateTime? time,
        AsyncMarkingBinaryReader reader,
        int i,
        CancellationToken cancellationToken
    )
    {
        var rmode = (uint)extendedFlags >> ((3 - i) * 4);
        if ((rmode & 8) == 0)
        {
            return null;
        }
        if (i != 0)
        {
            var dosTime = await reader.ReadUInt32Async(cancellationToken).ConfigureAwait(false);
            time = Utility.DosDateToDateTime(dosTime);
        }
        if ((rmode & 4) == 0 && time is not null)
        {
            time = time.Value.AddSeconds(1);
        }
        uint nanosecondHundreds = 0;
        var count = (int)rmode & 3;
        for (var j = 0; j < count; j++)
        {
            var b = await reader.ReadByteAsync(cancellationToken).ConfigureAwait(false);
            nanosecondHundreds |= (((uint)b) << ((j + 3 - count) * 8));
        }

        if (time is not null)
        {
            return time.Value.AddMilliseconds(nanosecondHundreds * Math.Pow(10, -4));
        }
        return null;
    }
}
