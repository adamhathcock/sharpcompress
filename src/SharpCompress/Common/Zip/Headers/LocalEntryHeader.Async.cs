using System.IO;
using System.Linq;
using System.Threading.Tasks;
using SharpCompress.IO;

namespace SharpCompress.Common.Zip.Headers;

internal partial class LocalEntryHeader
{
    internal override async ValueTask Read(AsyncBinaryReader reader)
    {
        Version = await reader.ReadUInt16Async().ConfigureAwait(false);
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
        var name = new byte[nameLength];
        var extra = new byte[extraLength];
        await reader.ReadBytesAsync(name, 0, nameLength).ConfigureAwait(false);
        await reader.ReadBytesAsync(extra, 0, extraLength).ConfigureAwait(false);

        ProcessReadData(name, extra);
    }
}
