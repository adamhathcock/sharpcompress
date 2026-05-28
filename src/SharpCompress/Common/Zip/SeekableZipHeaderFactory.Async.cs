using System;
using System.Buffers;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using SharpCompress.Common.Zip.Headers;
using SharpCompress.IO;

namespace SharpCompress.Common.Zip;

internal sealed partial class SeekableZipHeaderFactory
{
    internal async IAsyncEnumerable<ZipHeader> ReadSeekableHeaderAsync(Stream stream)
    {
#if NET8_0_OR_GREATER
        await using var reader = new AsyncBinaryReader(stream, leaveOpen: true);
#else
        using var reader = new AsyncBinaryReader(stream, leaveOpen: true);
#endif

        await SeekBackToHeaderAsync(stream, reader).ConfigureAwait(false);

        var eocd_location = stream.Position;
        var entry = new DirectoryEndHeader();
        await entry.Read(reader).ConfigureAwait(false);

        if (entry.IsZip64)
        {
            _zip64 = true;

            // ZIP64_END_OF_CENTRAL_DIRECTORY_LOCATOR should be before the EOCD
            stream.Seek(eocd_location - ZIP64_EOCD_LENGTH - 4, SeekOrigin.Begin);
            uint zip64_locator = await reader.ReadUInt32Async().ConfigureAwait(false);
            if (zip64_locator != ZIP64_END_OF_CENTRAL_DIRECTORY_LOCATOR)
            {
                throw new ArchiveException("Failed to locate the Zip64 Directory Locator");
            }

            var zip64Locator = new Zip64DirectoryEndLocatorHeader();
            await zip64Locator.Read(reader).ConfigureAwait(false);

            stream.Seek(zip64Locator.RelativeOffsetOfTheEndOfDirectoryRecord, SeekOrigin.Begin);
            var zip64Signature = await reader.ReadUInt32Async().ConfigureAwait(false);
            if (zip64Signature != ZIP64_END_OF_CENTRAL_DIRECTORY)
            {
                throw new ArchiveException("Failed to locate the Zip64 Header");
            }

            var zip64Entry = new Zip64DirectoryEndHeader();
            await zip64Entry.Read(reader).ConfigureAwait(false);
            stream.Seek(zip64Entry.DirectoryStartOffsetRelativeToDisk, SeekOrigin.Begin);
        }
        else
        {
            stream.Seek(entry.DirectoryStartOffsetRelativeToDisk, SeekOrigin.Begin);
        }

        var position = stream.Position;
        while (true)
        {
            stream.Position = position;
            var signature = await reader.ReadUInt32Async().ConfigureAwait(false);
            var nextHeader = await ReadHeader(signature, reader, _zip64).ConfigureAwait(false);
            position = stream.Position;

            if (nextHeader is null)
            {
                yield break;
            }

            if (nextHeader is DirectoryEntryHeader entryHeader)
            {
                //entry could be zero bytes so we need to know that.
                entryHeader.HasData = entryHeader.CompressedSize != 0;
                yield return entryHeader;
            }
            else if (nextHeader is DirectoryEndHeader endHeader)
            {
                yield return endHeader;
            }
        }
    }

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
            await reader.ReadBytesAsync(seek, 0, len, default).ConfigureAwait(false);
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
#if NET8_0_OR_GREATER
        await using var reader = new AsyncBinaryReader(stream, leaveOpen: true);
#else
        using var reader = new AsyncBinaryReader(stream, leaveOpen: true);
#endif
        var signature = await reader.ReadUInt32Async().ConfigureAwait(false);
        if (
            await ReadHeader(signature, reader, _zip64).ConfigureAwait(false)
            is not LocalEntryHeader localEntryHeader
        )
        {
            throw new ArchiveOperationException();
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
