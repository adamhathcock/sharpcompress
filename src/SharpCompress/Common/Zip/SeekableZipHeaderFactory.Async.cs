using System;
using System.Buffers;
using System.IO;
using System.Threading.Tasks;
using SharpCompress.Common.Zip.Headers;
using SharpCompress.IO;

namespace SharpCompress.Common.Zip;

internal sealed partial class SeekableZipHeaderFactory
{
    private static async ValueTask SeekBackToHeaderAsync(Stream stream, AsyncBinaryReader reader)
    {
        // Minimum EOCD length
        if (stream.Length < MINIMUM_EOCD_LENGTH)
        {
            throw new ArchiveException(
                "Could not find Zip file Directory at the end of the file. File may be corrupted."
            );
        }

        var len =
            stream.Length < MAX_SEARCH_LENGTH_FOR_EOCD
                ? (int)stream.Length
                : MAX_SEARCH_LENGTH_FOR_EOCD;

        stream.Seek(-len, SeekOrigin.End);
        var seek = ArrayPool<byte>.Shared.Rent(len);

        try
        {
            await reader.ReadBytesAsync(seek, 0, len, default);
            var memory = new Memory<byte>(seek, 0, len);
            var span = memory.Span;
            span.Reverse();

            // don't exclude the minimum eocd region, otherwise you fail to locate the header in empty zip files
            var max_search_area = len; // - MINIMUM_EOCD_LENGTH;

            for (var pos_from_end = 0; pos_from_end < max_search_area; ++pos_from_end)
            {
                if (IsMatch(span, pos_from_end, needle))
                {
                    stream.Seek(-pos_from_end, SeekOrigin.End);
                    return;
                }
            }

            throw new ArchiveException("Failed to locate the Zip Header");
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(seek);
        }
    }

    internal async ValueTask<LocalEntryHeader> GetLocalHeaderAsync(
        Stream stream,
        DirectoryEntryHeader directoryEntryHeader
    )
    {
        stream.Seek(directoryEntryHeader.RelativeOffsetOfEntryHeader, SeekOrigin.Begin);
        var reader = new AsyncBinaryReader(stream);
        var signature = await reader.ReadUInt32Async();
        if (await ReadHeader(signature, reader, _zip64) is not LocalEntryHeader localEntryHeader)
        {
            throw new InvalidOperationException();
        }

        // populate fields only known from the DirectoryEntryHeader
        localEntryHeader.HasData = directoryEntryHeader.HasData;
        localEntryHeader.ExternalFileAttributes = directoryEntryHeader.ExternalFileAttributes;
        localEntryHeader.Comment = directoryEntryHeader.Comment;

        if (FlagUtility.HasFlag(localEntryHeader.Flags, HeaderFlags.UsePostDataDescriptor))
        {
            localEntryHeader.Crc = directoryEntryHeader.Crc;
            localEntryHeader.CompressedSize = directoryEntryHeader.CompressedSize;
            localEntryHeader.UncompressedSize = directoryEntryHeader.UncompressedSize;
        }
        return localEntryHeader;
    }
}
