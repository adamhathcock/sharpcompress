using System;
using System.IO;

namespace SharpCompress.Common.Zip.Headers;

internal partial class Zip64DirectoryEndHeader : ZipHeader
{
    public Zip64DirectoryEndHeader()
        : base(ZipHeaderType.Zip64DirectoryEnd) { }

    internal override void Read(BinaryReader reader)
    {
        SizeOfDirectoryEndRecord = ValidateRecordSize(reader.ReadUInt64());
        VersionMadeBy = reader.ReadUInt16();
        VersionNeededToExtract = reader.ReadUInt16();
        VolumeNumber = reader.ReadUInt32();
        FirstVolumeWithDirectory = reader.ReadUInt32();
        TotalNumberOfEntriesInDisk = (long)reader.ReadUInt64();
        TotalNumberOfEntries = (long)reader.ReadUInt64();
        DirectorySize = (long)reader.ReadUInt64();
        DirectoryStartOffsetRelativeToDisk = (long)reader.ReadUInt64();
        // Extensible data is not interpreted. Consume it without allocating from its declared size.
        var remaining =
            SizeOfDirectoryEndRecord - SIZE_OF_FIXED_HEADER_DATA_EXCEPT_SIGNATURE_AND_SIZE_FIELDS;
        var buffer = new byte[4096];
        while (remaining > 0)
        {
            var read = reader.Read(buffer, 0, (int)Math.Min(buffer.Length, remaining));
            if (read == 0)
            {
                throw new IncompleteArchiveException("Truncated ZIP64 extensible data.");
            }
            remaining -= read;
        }
    }

    private static long ValidateRecordSize(ulong size)
    {
        if (
            size < SIZE_OF_FIXED_HEADER_DATA_EXCEPT_SIGNATURE_AND_SIZE_FIELDS
            || size > long.MaxValue
        )
        {
            throw new InvalidFormatException("Invalid ZIP64 end record size.");
        }
        return (long)size;
    }

    private const int SIZE_OF_FIXED_HEADER_DATA_EXCEPT_SIGNATURE_AND_SIZE_FIELDS = 44;

    public long SizeOfDirectoryEndRecord { get; private set; }

    public ushort VersionMadeBy { get; private set; }

    public ushort VersionNeededToExtract { get; private set; }

    public uint VolumeNumber { get; private set; }

    public uint FirstVolumeWithDirectory { get; private set; }

    public long TotalNumberOfEntriesInDisk { get; private set; }

    public long TotalNumberOfEntries { get; private set; }

    public long DirectorySize { get; private set; }

    public long DirectoryStartOffsetRelativeToDisk { get; private set; }
}
