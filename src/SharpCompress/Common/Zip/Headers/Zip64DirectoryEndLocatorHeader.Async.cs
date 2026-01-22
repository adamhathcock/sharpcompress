using System.IO;
using System.Threading.Tasks;

namespace SharpCompress.Common.Zip.Headers;

internal partial class Zip64DirectoryEndLocatorHeader
{
    internal override async ValueTask Read(AsyncBinaryReader reader)
    {
        FirstVolumeWithDirectory = await reader.ReadUInt32Async();
        RelativeOffsetOfTheEndOfDirectoryRecord = (long)await reader.ReadUInt64Async();
        TotalNumberOfVolumes = await reader.ReadUInt32Async();
    }
}
