using System;
using System.IO;
using System.Text;

namespace SharpCompress.Common.Ace;

/// <summary>
/// Represents header information for an ACE archive entry.
/// ACE format uses little-endian byte ordering.
/// </summary>
public class AceEntryHeader
{
    // Header type constants
    private const byte HeaderTypeMain = 0;
    private const byte HeaderTypeFile = 1;

    // Header flags
    private const ushort FlagAddSize = 0x0001;
    private const ushort FlagComment = 0x0002;
    private const ushort FlagSolid = 0x8000;

    public ArchiveEncoding ArchiveEncoding { get; }
    public CompressionType CompressionMethod { get; private set; }
    public string? Name { get; private set; }
    public long CompressedSize { get; private set; }
    public long OriginalSize { get; private set; }
    public DateTime DateTime { get; private set; }
    internal uint Crc32 { get; private set; }
    public int FileAttributes { get; private set; }
    public long DataStartPosition { get; private set; }
    public bool IsDirectory { get; private set; }

    public AceEntryHeader(ArchiveEncoding archiveEncoding)
    {
        ArchiveEncoding = archiveEncoding;
    }

    /// <summary>
    /// Reads the main archive header from the stream.
    /// Returns true if this is a valid ACE archive.
    /// </summary>
    public bool ReadMainHeader(Stream stream)
    {
        // Read header CRC (2 bytes) and header size (2 bytes)
        var headerPrefix = new byte[4];
        if (stream.Read(headerPrefix, 0, 4) != 4)
        {
            return false;
        }

        int headerSize = BitConverter.ToUInt16(headerPrefix, 2);
        if (headerSize < 1)
        {
            return false;
        }

        // Read the rest of the header
        var headerData = new byte[headerSize];
        if (stream.Read(headerData, 0, headerSize) != headerSize)
        {
            return false;
        }

        // Header type should be 0 for main header
        if (headerData[0] != HeaderTypeMain)
        {
            return false;
        }

        return true;
    }

    /// <summary>
    /// Reads the next file entry header from the stream.
    /// Returns null if no more entries or end of archive.
    /// </summary>
    public AceEntryHeader? ReadHeader(Stream stream)
    {
        // Read header CRC (2 bytes) and header size (2 bytes)
        var headerPrefix = new byte[4];
        int bytesRead = stream.Read(headerPrefix, 0, 4);
        if (bytesRead < 4)
        {
            return null; // End of archive
        }

        // ushort headerCrc = BitConverter.ToUInt16(headerPrefix, 0); // CRC for validation
        ushort headerSize = BitConverter.ToUInt16(headerPrefix, 2);

        if (headerSize == 0)
        {
            return null; // End of archive marker
        }

        // Read the header data
        var headerData = new byte[headerSize];
        if (stream.Read(headerData, 0, headerSize) != headerSize)
        {
            return null;
        }

        int offset = 0;

        // Header type (1 byte)
        byte headerType = headerData[offset++];

        // Check for end marker or non-file header
        if (headerType == HeaderTypeMain)
        {
            // Skip main header if encountered
            return ReadHeader(stream);
        }

        if (headerType != HeaderTypeFile)
        {
            // Unknown header type - skip
            return ReadHeader(stream);
        }

        // Header flags (2 bytes)
        ushort headerFlags = BitConverter.ToUInt16(headerData, offset);
        offset += 2;

        // Packed size (4 bytes)
        CompressedSize = BitConverter.ToUInt32(headerData, offset);
        offset += 4;

        // Original size (4 bytes)
        OriginalSize = BitConverter.ToUInt32(headerData, offset);
        offset += 4;

        // File date/time in DOS format (4 bytes)
        uint dosDateTime = BitConverter.ToUInt32(headerData, offset);
        DateTime = ConvertDosDateTime(dosDateTime);
        offset += 4;

        // File attributes (4 bytes)
        FileAttributes = (int)BitConverter.ToUInt32(headerData, offset);
        offset += 4;

        // CRC32 (4 bytes)
        Crc32 = BitConverter.ToUInt32(headerData, offset);
        offset += 4;

        // Compression type (1 byte)
        byte compressionType = headerData[offset++];
        CompressionMethod = GetCompressionType(compressionType);

        // Compression quality/parameter (1 byte) - skip
        offset++;

        // Parameters (2 bytes) - skip
        offset += 2;

        // Reserved (2 bytes) - skip
        offset += 2;

        // Filename length (2 bytes)
        ushort filenameLength = BitConverter.ToUInt16(headerData, offset);
        offset += 2;

        // Filename
        if (offset + filenameLength <= headerData.Length)
        {
            Name = ArchiveEncoding.Decode(headerData, offset, filenameLength);
            offset += filenameLength;
        }

        // Check if entry is a directory based on attributes or name
        IsDirectory = (FileAttributes & 0x10) != 0 || (Name?.EndsWith('/') ?? false);

        // Handle comment if present
        if ((headerFlags & FlagComment) != 0)
        {
            // Comment length (2 bytes)
            if (offset + 2 <= headerData.Length)
            {
                ushort commentLength = BitConverter.ToUInt16(headerData, offset);
                offset += 2 + commentLength; // Skip comment
            }
        }

        // Store the data start position
        DataStartPosition = stream.Position;

        return this;
    }

    private static CompressionType GetCompressionType(byte value)
    {
        return value switch
        {
            0 => CompressionType.None, // Stored
            1 => CompressionType.Lzw, // LZ77 - closest equivalent
            2 => CompressionType.Lzw, // ACE v2.0 compression
            _ => CompressionType.Unknown,
        };
    }

    /// <summary>
    /// Converts DOS date/time format to DateTime.
    /// </summary>
    private static DateTime ConvertDosDateTime(uint dosDateTime)
    {
        try
        {
            int second = (int)(dosDateTime & 0x1F) * 2;
            int minute = (int)((dosDateTime >> 5) & 0x3F);
            int hour = (int)((dosDateTime >> 11) & 0x1F);
            int day = (int)((dosDateTime >> 16) & 0x1F);
            int month = (int)((dosDateTime >> 21) & 0x0F);
            int year = (int)((dosDateTime >> 25) & 0x7F) + 1980;

            if (
                day < 1
                || day > 31
                || month < 1
                || month > 12
                || hour > 23
                || minute > 59
                || second > 59
            )
            {
                return DateTime.MinValue;
            }

            return new DateTime(year, month, day, hour, minute, second);
        }
        catch
        {
            return DateTime.MinValue;
        }
    }
}
