using System.IO;
using System.Threading.Tasks;

namespace SharpCompress.Common.Zip.Headers;

internal class Zip64DirectoryEndLocatorHeader() : ZipHeader(ZipHeaderType.Zip64DirectoryEndLocator)
{
    internal override async ValueTask  Read(AsyncBinaryReader reader)
    {
        FirstVolumeWithDirectory = await reader.ReadUInt32Async();
        RelativeOffsetOfTheEndOfDirectoryRecord = (long)await reader.ReadUInt64Async();
        TotalNumberOfVolumes = await reader.ReadUInt32Async();
    }

    public uint FirstVolumeWithDirectory { get; private set; }

    public long RelativeOffsetOfTheEndOfDirectoryRecord { get; private set; }

    public uint TotalNumberOfVolumes { get; private set; }
}
