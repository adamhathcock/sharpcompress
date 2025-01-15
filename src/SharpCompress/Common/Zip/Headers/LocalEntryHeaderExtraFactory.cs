using System;
using System.Buffers.Binary;
using System.Text;

namespace SharpCompress.Common.Zip.Headers;

internal enum ExtraDataType : ushort
{
    WinZipAes = 0x9901,

    NotImplementedExtraData = 0xFFFF,

    // Third Party Mappings
    // -Info-ZIP Unicode Path Extra Field
    UnicodePathExtraField = 0x7075,
    Zip64ExtendedInformationExtraField = 0x0001,
    UnixTimeExtraField = 0x5455,
}

internal class ExtraData
{
    public ExtraData(ExtraDataType type, ushort length, byte[] dataBytes)
    {
        Type = type;
        Length = length;
        DataBytes = dataBytes;
    }

    internal ExtraDataType Type { get; }
    internal ushort Length { get; }
    internal byte[] DataBytes { get; }
}

internal sealed class ExtraUnicodePathExtraField : ExtraData
{
    public ExtraUnicodePathExtraField(ExtraDataType type, ushort length, byte[] dataBytes)
        : base(type, length, dataBytes) { }

    internal byte Version => DataBytes[0];

    internal byte[] NameCrc32
    {
        get
        {
            var crc = new byte[4];
            Buffer.BlockCopy(DataBytes, 1, crc, 0, 4);
            return crc;
        }
    }

    internal string UnicodeName
    {
        get
        {
            // PathNamelength = dataLength - Version(1 byte) - NameCRC32(4 bytes)
            var length = Length - 5;
            var nameStr = Encoding.UTF8.GetString(DataBytes, 5, length);
            return nameStr;
        }
    }
}

internal sealed class Zip64ExtendedInformationExtraField : ExtraData
{
    public Zip64ExtendedInformationExtraField(ExtraDataType type, ushort length, byte[] dataBytes)
        : base(type, length, dataBytes) { }

    // From the spec, values are only in the extradata if the standard
    // value is set to 0xFFFFFFFF (or 0xFFFF for the Disk Start Number).
    // Values, if present, must appear in the following order:
    // - Original Size
    // - Compressed Size
    // - Relative Header Offset
    // - Disk Start Number
    public void Process(
        long uncompressedFileSize,
        long compressedFileSize,
        long relativeHeaderOffset,
        ushort diskNumber
    )
    {
        var bytesRequired =
            ((uncompressedFileSize == uint.MaxValue) ? 8 : 0)
            + ((compressedFileSize == uint.MaxValue) ? 8 : 0)
            + ((relativeHeaderOffset == uint.MaxValue) ? 8 : 0)
            + ((diskNumber == ushort.MaxValue) ? 4 : 0);
        var currentIndex = 0;

        if (bytesRequired > DataBytes.Length)
        {
            throw new ArchiveException(
                "Zip64 extended information extra field is not large enough for the required information"
            );
        }

        if (uncompressedFileSize == uint.MaxValue)
        {
            UncompressedSize = BinaryPrimitives.ReadInt64LittleEndian(
                DataBytes.AsSpan(currentIndex)
            );
            currentIndex += 8;
        }

        if (compressedFileSize == uint.MaxValue)
        {
            CompressedSize = BinaryPrimitives.ReadInt64LittleEndian(DataBytes.AsSpan(currentIndex));
            currentIndex += 8;
        }

        if (relativeHeaderOffset == uint.MaxValue)
        {
            RelativeOffsetOfEntryHeader = BinaryPrimitives.ReadInt64LittleEndian(
                DataBytes.AsSpan(currentIndex)
            );
            currentIndex += 8;
        }

        if (diskNumber == ushort.MaxValue)
        {
            VolumeNumber = BinaryPrimitives.ReadUInt32LittleEndian(DataBytes.AsSpan(currentIndex));
        }
    }

    /// <summary>
    /// Uncompressed file size. Only valid after <see cref="Process(long, long, long, ushort)"/> has been called and if the
    /// original entry header had a corresponding 0xFFFFFFFF value.
    /// </summary>
    public long UncompressedSize { get; private set; }

    /// <summary>
    /// Compressed file size. Only valid after <see cref="Process(long, long, long, ushort)"/> has been called and if the
    /// original entry header had a corresponding 0xFFFFFFFF value.
    /// </summary>
    public long CompressedSize { get; private set; }

    /// <summary>
    /// Relative offset of the entry header. Only valid after <see cref="Process(long, long, long, ushort)"/> has been called and if the
    /// original entry header had a corresponding 0xFFFFFFFF value.
    /// </summary>
    public long RelativeOffsetOfEntryHeader { get; private set; }

    /// <summary>
    /// Volume number. Only valid after <see cref="Process(long, long, long, ushort)"/> has been called and if the
    /// original entry header had a corresponding 0xFFFF value.
    /// </summary>
    public uint VolumeNumber { get; private set; }
}

internal sealed class UnixTimeExtraField : ExtraData
{
    public UnixTimeExtraField(ExtraDataType type, ushort length, byte[] dataBytes)
        : base(type, length, dataBytes) { }

    /// <summary>
    /// The unix modified time, last access time, and creation time, if set.
    /// </summary>
    /// <remarks>Must return Tuple explicitly due to net462 support.</remarks>
    internal Tuple<DateTime?, DateTime?, DateTime?> UnicodeTimes
    {
        get
        {
            // There has to be at least 5 byte for there to be a timestamp.
            // 1 byte for flags and 4 bytes for a timestamp.
            if (DataBytes is null || DataBytes.Length < 5)
            {
                return Tuple.Create<DateTime?, DateTime?, DateTime?>(null, null, null);
            }

            var flags = (RecordedTimeFlag)DataBytes[0];
            var isModifiedTimeSpecified = flags.HasFlag(RecordedTimeFlag.LastModified);
            var isLastAccessTimeSpecified = flags.HasFlag(RecordedTimeFlag.LastAccessed);
            var isCreationTimeSpecified = flags.HasFlag(RecordedTimeFlag.Created);
            var currentIndex = 1;
            DateTime? modifiedTime = null;
            DateTime? lastAccessTime = null;
            DateTime? creationTime = null;

            if (isModifiedTimeSpecified)
            {
                var modifiedEpochTime = BinaryPrimitives.ReadInt32LittleEndian(
                    DataBytes.AsSpan(currentIndex, 4)
                );

                currentIndex += 4;
                modifiedTime = DateTimeOffset.FromUnixTimeSeconds(modifiedEpochTime).UtcDateTime;
            }

            if (isLastAccessTimeSpecified)
            {
                if (currentIndex + 4 > DataBytes.Length)
                {
                    return Tuple.Create<DateTime?, DateTime?, DateTime?>(null, null, null);
                }

                var lastAccessEpochTime = BinaryPrimitives.ReadInt32LittleEndian(
                    DataBytes.AsSpan(currentIndex, 4)
                );

                currentIndex += 4;
                lastAccessTime = DateTimeOffset
                    .FromUnixTimeSeconds(lastAccessEpochTime)
                    .UtcDateTime;
            }

            if (isCreationTimeSpecified)
            {
                if (currentIndex + 4 > DataBytes.Length)
                {
                    return Tuple.Create<DateTime?, DateTime?, DateTime?>(null, null, null);
                }

                var creationTimeEpochTime = BinaryPrimitives.ReadInt32LittleEndian(
                    DataBytes.AsSpan(currentIndex, 4)
                );

                currentIndex += 4;
                creationTime = DateTimeOffset
                    .FromUnixTimeSeconds(creationTimeEpochTime)
                    .UtcDateTime;
            }

            return Tuple.Create(modifiedTime, lastAccessTime, creationTime);
        }
    }

    [Flags]
    private enum RecordedTimeFlag
    {
        None = 0,
        LastModified = 1,
        LastAccessed = 2,
        Created = 4,
    }
}

internal static class LocalEntryHeaderExtraFactory
{
    internal static ExtraData Create(ExtraDataType type, ushort length, byte[] extraData) =>
        type switch
        {
            ExtraDataType.UnicodePathExtraField => new ExtraUnicodePathExtraField(
                type,
                length,
                extraData
            ),
            ExtraDataType.Zip64ExtendedInformationExtraField =>
                new Zip64ExtendedInformationExtraField(type, length, extraData),
            ExtraDataType.UnixTimeExtraField => new UnixTimeExtraField(type, length, extraData),
            _ => new ExtraData(type, length, extraData),
        };
}
