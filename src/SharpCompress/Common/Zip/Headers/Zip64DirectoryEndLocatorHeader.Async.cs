using System.IO;
using System.Threading.Tasks;
using SharpCompress.IO;

namespace SharpCompress.Common.Zip.Headers;

internal partial class Zip64DirectoryEndLocatorHeader
{
    internal override async ValueTask Read(AsyncBinaryReader reader)
    {
        FirstVolumeWithDirectory = await reader.ReadUInt32Async().ConfigureAwait(false);
        RelativeOffsetOfTheEndOfDirectoryRecord = (long)
            await reader.ReadUInt64Async().ConfigureAwait(false);
        TotalNumberOfVolumes = await reader.ReadUInt32Async().ConfigureAwait(false);
    }
}
