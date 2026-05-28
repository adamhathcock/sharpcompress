using System.IO;
using System.Linq;
using System.Threading.Tasks;
using SharpCompress.Common;
using SharpCompress.IO;

namespace SharpCompress.Common.Zip.Headers;

internal partial class DirectoryEntryHeader
{
    internal override async ValueTask Read(AsyncBinaryReader reader)
    {
        Version = await reader.ReadUInt16Async().ConfigureAwait(false);
        VersionNeededToExtract = await reader.ReadUInt16Async().ConfigureAwait(false);
        Flags = (HeaderFlags)await reader.ReadUInt16Async().ConfigureAwait(false);
        CompressionMethod = (ZipCompressionMethod)
            await reader.ReadUInt16Async().ConfigureAwait(false);
        OriginalLastModifiedTime = LastModifiedTime = await reader
            .ReadUInt16Async()
            .ConfigureAwait(false);
        OriginalLastModifiedDate = LastModifiedDate = await reader
            .ReadUInt16Async()
            .ConfigureAwait(false);
        Crc = await reader.ReadUInt32Async().ConfigureAwait(false);
        CompressedSize = await reader.ReadUInt32Async().ConfigureAwait(false);
        UncompressedSize = await reader.ReadUInt32Async().ConfigureAwait(false);
        var nameLength = await reader.ReadUInt16Async().ConfigureAwait(false);
        var extraLength = await reader.ReadUInt16Async().ConfigureAwait(false);
        var commentLength = await reader.ReadUInt16Async().ConfigureAwait(false);
        DiskNumberStart = await reader.ReadUInt16Async().ConfigureAwait(false);
        InternalFileAttributes = await reader.ReadUInt16Async().ConfigureAwait(false);
        ExternalFileAttributes = await reader.ReadUInt32Async().ConfigureAwait(false);
        RelativeOffsetOfEntryHeader = await reader.ReadUInt32Async().ConfigureAwait(false);
        var name = new byte[nameLength];
        var extra = new byte[extraLength];
        var comment = new byte[commentLength];
        await reader.ReadBytesAsync(name, 0, nameLength).ConfigureAwait(false);
        await reader.ReadBytesAsync(extra, 0, extraLength).ConfigureAwait(false);
        await reader.ReadBytesAsync(comment, 0, commentLength).ConfigureAwait(false);

        ProcessReadData(name, extra, comment);
    }
}
