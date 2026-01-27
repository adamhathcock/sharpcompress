using System.IO;
using System.Linq;
using System.Threading.Tasks;
using SharpCompress.Common;

namespace SharpCompress.Common.Zip.Headers;

internal partial class DirectoryEntryHeader
{
    internal override async ValueTask Read(AsyncBinaryReader reader)
    {
        Version = await reader.ReadUInt16Async();
        VersionNeededToExtract = await reader.ReadUInt16Async();
        Flags = (HeaderFlags)await reader.ReadUInt16Async();
        CompressionMethod = (ZipCompressionMethod)await reader.ReadUInt16Async();
        OriginalLastModifiedTime = LastModifiedTime = await reader.ReadUInt16Async();
        OriginalLastModifiedDate = LastModifiedDate = await reader.ReadUInt16Async();
        Crc = await reader.ReadUInt32Async();
        CompressedSize = await reader.ReadUInt32Async();
        UncompressedSize = await reader.ReadUInt32Async();
        var nameLength = await reader.ReadUInt16Async();
        var extraLength = await reader.ReadUInt16Async();
        var commentLength = await reader.ReadUInt16Async();
        DiskNumberStart = await reader.ReadUInt16Async();
        InternalFileAttributes = await reader.ReadUInt16Async();
        ExternalFileAttributes = await reader.ReadUInt32Async();
        RelativeOffsetOfEntryHeader = await reader.ReadUInt32Async();
        var name = new byte[nameLength];
        var extra = new byte[extraLength];
        var comment = new byte[commentLength];
        await reader.ReadBytesAsync(name, 0, nameLength);
        await reader.ReadBytesAsync(extra, 0, extraLength);
        await reader.ReadBytesAsync(comment, 0, commentLength);

        ProcessReadData(name, extra, comment);
    }
}
