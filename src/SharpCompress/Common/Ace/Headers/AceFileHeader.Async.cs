using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using SharpCompress.Common.Arc;

namespace SharpCompress.Common.Ace.Headers;

public sealed partial class AceFileHeader
{
    /// <summary>
    /// Asynchronously reads the next file entry header from the stream.
    /// Returns null if no more entries or end of archive.
    /// Supports both ACE 1.0 and ACE 2.0 formats.
    /// </summary>
    public override async ValueTask<AceHeader?> ReadAsync(
        Stream reader,
        CancellationToken cancellationToken = default
    )
    {
        var headerData = await ReadHeaderAsync(reader, cancellationToken).ConfigureAwait(false);
        if (headerData.Length == 0)
        {
            return null;
        }
        int offset = 0;

        // Header type (1 byte)
        HeaderType = headerData[offset++];

        // Skip recovery record headers (ACE 2.0 feature)
        if (HeaderType == (byte)SharpCompress.Common.Ace.Headers.AceHeaderType.RECOVERY32)
        {
            // Skip to next header
            return null;
        }

        if (HeaderType != (byte)SharpCompress.Common.Ace.Headers.AceHeaderType.FILE)
        {
            // Unknown header type - skip
            return null;
        }

        // Header flags (2 bytes)
        HeaderFlags = BitConverter.ToUInt16(headerData, offset);
        offset += 2;

        // Packed size (4 bytes)
        PackedSize = BitConverter.ToUInt32(headerData, offset);
        offset += 4;

        // Original size (4 bytes)
        OriginalSize = BitConverter.ToUInt32(headerData, offset);
        offset += 4;

        // File date/time in DOS format (4 bytes)
        var dosDateTime = BitConverter.ToUInt32(headerData, offset);
        DateTime = ConvertDosDateTime(dosDateTime);
        offset += 4;

        // File attributes (4 bytes)
        Attributes = (int)BitConverter.ToUInt32(headerData, offset);
        offset += 4;

        // CRC32 (4 bytes)
        Crc32 = BitConverter.ToUInt32(headerData, offset);
        offset += 4;

        // Compression type (1 byte)
        byte compressionType = headerData[offset++];
        CompressionType = GetCompressionType(compressionType);

        // Compression quality/parameter (1 byte)
        byte compressionQuality = headerData[offset++];
        CompressionQuality = GetCompressionQuality(compressionQuality);

        // Parameters (2 bytes)
        Parameters = BitConverter.ToUInt16(headerData, offset);
        offset += 2;

        // Reserved (2 bytes) - skip
        offset += 2;

        // Filename length (2 bytes)
        var filenameLength = BitConverter.ToUInt16(headerData, offset);
        offset += 2;

        // Filename
        if (offset + filenameLength <= headerData.Length)
        {
            Filename = ArchiveEncoding.Decode(headerData, offset, filenameLength);
            offset += filenameLength;
        }

        // Handle comment if present
        if ((HeaderFlags & SharpCompress.Common.Ace.Headers.HeaderFlags.COMMENT) != 0)
        {
            // Comment length (2 bytes)
            if (offset + 2 <= headerData.Length)
            {
                ushort commentLength = BitConverter.ToUInt16(headerData, offset);
                offset += 2 + commentLength; // Skip comment
            }
        }

        // Store the data start position
        DataStartPosition = reader.Position;

        return this;
    }
}
