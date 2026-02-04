using System.IO;
using System.Linq;
using System.Threading.Tasks;
using SharpCompress.IO;

namespace SharpCompress.Common.Zip.Headers;

internal partial class LocalEntryHeader
{
    internal override async ValueTask Read(AsyncBinaryReader reader)
    {
        Version = await reader.ReadUInt16Async();
        Flags = (HeaderFlags)await reader.ReadUInt16Async();
        CompressionMethod = (ZipCompressionMethod)await reader.ReadUInt16Async();
        OriginalLastModifiedTime = LastModifiedTime = await reader.ReadUInt16Async();
        OriginalLastModifiedDate = LastModifiedDate = await reader.ReadUInt16Async();
        Crc = await reader.ReadUInt32Async();
        CompressedSize = await reader.ReadUInt32Async();
        UncompressedSize = await reader.ReadUInt32Async();
        var nameLength = await reader.ReadUInt16Async();
        var extraLength = await reader.ReadUInt16Async();
        var name = new byte[nameLength];
        var extra = new byte[extraLength];
        await reader.ReadBytesAsync(name, 0, nameLength);
        await reader.ReadBytesAsync(extra, 0, extraLength);

        ProcessReadData(name, extra);
    }
}
