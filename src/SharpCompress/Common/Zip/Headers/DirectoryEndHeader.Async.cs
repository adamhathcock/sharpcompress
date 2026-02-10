using System.IO;
using System.Threading.Tasks;
using SharpCompress.IO;

namespace SharpCompress.Common.Zip.Headers;

internal partial class DirectoryEndHeader
{
    internal override async ValueTask Read(AsyncBinaryReader reader)
    {
        VolumeNumber = await reader.ReadUInt16Async().ConfigureAwait(false);
        FirstVolumeWithDirectory = await reader.ReadUInt16Async().ConfigureAwait(false);
        TotalNumberOfEntriesInDisk = await reader.ReadUInt16Async().ConfigureAwait(false);
        TotalNumberOfEntries = await reader.ReadUInt16Async().ConfigureAwait(false);
        DirectorySize = await reader.ReadUInt32Async().ConfigureAwait(false);
        DirectoryStartOffsetRelativeToDisk = await reader.ReadUInt32Async().ConfigureAwait(false);
        CommentLength = await reader.ReadUInt16Async().ConfigureAwait(false);
        Comment = new byte[CommentLength];
        await reader.ReadBytesAsync(Comment, 0, CommentLength).ConfigureAwait(false);
    }
}
