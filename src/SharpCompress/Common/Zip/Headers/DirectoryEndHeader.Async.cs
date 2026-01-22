using System.IO;
using System.Threading.Tasks;

namespace SharpCompress.Common.Zip.Headers;

internal partial class DirectoryEndHeader
{
    internal override async ValueTask Read(AsyncBinaryReader reader)
    {
        VolumeNumber = await reader.ReadUInt16Async();
        FirstVolumeWithDirectory = await reader.ReadUInt16Async();
        TotalNumberOfEntriesInDisk = await reader.ReadUInt16Async();
        TotalNumberOfEntries = await reader.ReadUInt16Async();
        DirectorySize = await reader.ReadUInt32Async();
        DirectoryStartOffsetRelativeToDisk = await reader.ReadUInt32Async();
        CommentLength = await reader.ReadUInt16Async();
        Comment = new byte[CommentLength];
        await reader.ReadBytesAsync(Comment, 0, CommentLength);
    }
}
