using System.IO;
using System.Threading.Tasks;

namespace SharpCompress.Common.Zip.Headers;

internal partial class Zip64DirectoryEndHeader
{
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
}
