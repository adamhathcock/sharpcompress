using System.IO;
using System.Threading.Tasks;

namespace SharpCompress.Common.Zip.Headers;

internal class Zip64DirectoryEndHeader : ZipHeader
{
    public Zip64DirectoryEndHeader()
        : base(ZipHeaderType.Zip64DirectoryEnd) { }

    internal override void Read(BinaryReader reader)
    {
        SizeOfDirectoryEndRecord = (long)reader.ReadUInt64();
        VersionMadeBy = reader.ReadUInt16();
        VersionNeededToExtract = reader.ReadUInt16();
        VolumeNumber = reader.ReadUInt32();
        FirstVolumeWithDirectory = reader.ReadUInt32();
        TotalNumberOfEntriesInDisk = (long)reader.ReadUInt64();
        TotalNumberOfEntries = (long)reader.ReadUInt64();
        DirectorySize = (long)reader.ReadUInt64();
        DirectoryStartOffsetRelativeToDisk = (long)reader.ReadUInt64();
        DataSector = reader.ReadBytes(
            (int)(
                SizeOfDirectoryEndRecord
                - SIZE_OF_FIXED_HEADER_DATA_EXCEPT_SIGNATURE_AND_SIZE_FIELDS
            )
        );
    }

    internal override async ValueTask Read(AsyncBinaryReader reader)
    {
        SizeOfDirectoryEndRecord = (long)await reader.ReadUInt64Async();
        VersionMadeBy = await reader.ReadUInt16Async();
        VersionNeededToExtract = await reader.ReadUInt16Async();
        VolumeNumber = await reader.ReadUInt32Async();
        FirstVolumeWithDirectory = await reader.ReadUInt32Async();
        TotalNumberOfEntriesInDisk = (long)await reader.ReadUInt64Async();
        TotalNumberOfEntries = (long)await reader.ReadUInt64Async();
        DirectorySize = (long)await reader.ReadUInt64Async();
        DirectoryStartOffsetRelativeToDisk = (long)await reader.ReadUInt64Async();
        var size = (int)(
            SizeOfDirectoryEndRecord - SIZE_OF_FIXED_HEADER_DATA_EXCEPT_SIGNATURE_AND_SIZE_FIELDS
        );
        DataSector = new byte[size];
        await reader.ReadBytesAsync(DataSector, 0, size);
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

    public byte[]? DataSector { get; private set; }
}
