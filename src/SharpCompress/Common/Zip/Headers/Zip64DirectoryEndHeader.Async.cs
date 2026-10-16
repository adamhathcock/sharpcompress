using System;
using System.IO;
using System.Threading.Tasks;
using SharpCompress.IO;

namespace SharpCompress.Common.Zip.Headers;

internal partial class Zip64DirectoryEndHeader
{
    internal override async ValueTask Read(AsyncBinaryReader reader)
    {
        SizeOfDirectoryEndRecord = ValidateRecordSize(
            await reader.ReadUInt64Async().ConfigureAwait(false)
        );
        VersionMadeBy = await reader.ReadUInt16Async().ConfigureAwait(false);
        VersionNeededToExtract = await reader.ReadUInt16Async().ConfigureAwait(false);
        VolumeNumber = await reader.ReadUInt32Async().ConfigureAwait(false);
        FirstVolumeWithDirectory = await reader.ReadUInt32Async().ConfigureAwait(false);
        TotalNumberOfEntriesInDisk = (long)await reader.ReadUInt64Async().ConfigureAwait(false);
        TotalNumberOfEntries = (long)await reader.ReadUInt64Async().ConfigureAwait(false);
        DirectorySize = (long)await reader.ReadUInt64Async().ConfigureAwait(false);
        DirectoryStartOffsetRelativeToDisk = (long)
            await reader.ReadUInt64Async().ConfigureAwait(false);
        var remaining =
            SizeOfDirectoryEndRecord - SIZE_OF_FIXED_HEADER_DATA_EXCEPT_SIGNATURE_AND_SIZE_FIELDS;
        var buffer = new byte[4096];
        while (remaining > 0)
        {
            var size = (int)Math.Min(buffer.Length, remaining);
            await reader.ReadBytesAsync(buffer, 0, size).ConfigureAwait(false);
            remaining -= size;
        }
    }
}
