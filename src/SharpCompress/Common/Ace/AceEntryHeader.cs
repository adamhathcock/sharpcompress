using System;
using System.IO;
using System.Text;

namespace SharpCompress.Common.Ace;

/// <summary>
/// Represents header information for an ACE archive entry.
/// ACE format uses little-endian byte ordering.
/// Supports both ACE 1.0 and ACE 2.0 formats.
/// </summary>
public class AceEntryHeader
{
    // Header type constants
    private const byte HeaderTypeMain = 0;
    private const byte HeaderTypeFile = 1;
    private const byte HeaderTypeRecovery = 2;

    // Header flags for main header
    private const ushort MainFlagComment = 0x0002;
    private const ushort MainFlagSfx = 0x0200;
    private const ushort MainFlagLocked = 0x0400;
    private const ushort MainFlagSolid = 0x0800;
    private const ushort MainFlagMultiVolume = 0x1000;
    private const ushort MainFlagAv = 0x2000;
    private const ushort MainFlagRecovery = 0x4000;

    // Header flags for file header
    private const ushort FileFlagAddSize = 0x0001;
    private const ushort FileFlagComment = 0x0002;
    private const ushort FileFlagContinued = 0x4000;
    private const ushort FileFlagContinuing = 0x8000;

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

    /// <summary>
    /// Gets the ACE archive version (10 for ACE 1.0, 20 for ACE 2.0).
    /// </summary>
    public byte AceVersion { get; private set; }

    /// <summary>
    /// Gets whether this is an ACE 2.0 archive.
    /// </summary>
    public bool IsAce20 => AceVersion >= 20;

    /// <summary>
    /// Gets the host operating system that created the archive.
    /// </summary>
    public byte HostOs { get; private set; }

    /// <summary>
    /// Gets whether the archive is solid.
    /// </summary>
    public bool IsSolid { get; private set; }

    /// <summary>
    /// Gets whether the archive is part of a multi-volume set.
    /// </summary>
    public bool IsMultiVolume { get; private set; }

    public AceEntryHeader(ArchiveEncoding archiveEncoding)
    {
        ArchiveEncoding = archiveEncoding;
    }

    /// <summary>
    /// Reads the main archive header from the stream.
    /// Returns true if this is a valid ACE archive.
    /// Supports both ACE 1.0 and ACE 2.0 formats.
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

        int offset = 0;

        // Header type should be 0 for main header
        if (headerData[offset++] != HeaderTypeMain)
        {
            return false;
        }

        // Header flags (2 bytes)
        ushort headerFlags = BitConverter.ToUInt16(headerData, offset);
        offset += 2;

        IsSolid = (headerFlags & MainFlagSolid) != 0;
        IsMultiVolume = (headerFlags & MainFlagMultiVolume) != 0;

        // Skip signature "**ACE**" (7 bytes)
        offset += 7;

        // ACE version (1 byte) - 10 for ACE 1.0, 20 for ACE 2.0
        if (offset < headerData.Length)
        {
            AceVersion = headerData[offset++];
        }

        // Extract version needed (1 byte)
        if (offset < headerData.Length)
        {
            offset++; // Skip version needed
        }

        // Host OS (1 byte)
        if (offset < headerData.Length)
        {
            HostOs = headerData[offset++];
        }

        // Volume number (1 byte)
        if (offset < headerData.Length)
        {
            offset++; // Skip volume number
        }

        // Creation date/time (4 bytes)
        if (offset + 4 <= headerData.Length)
        {
            offset += 4; // Skip datetime
        }

        // Reserved fields (8 bytes)
        if (offset + 8 <= headerData.Length)
        {
            offset += 8;
        }

        // Skip additional fields based on flags
        // Handle comment if present
        if ((headerFlags & MainFlagComment) != 0)
        {
            if (offset + 2 <= headerData.Length)
            {
                ushort commentLength = BitConverter.ToUInt16(headerData, offset);
                offset += 2 + commentLength;
            }
        }

        return true;
    }

    /// <summary>
    /// Reads the next file entry header from the stream.
    /// Returns null if no more entries or end of archive.
    /// Supports both ACE 1.0 and ACE 2.0 formats.
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

        // Skip recovery record headers (ACE 2.0 feature)
        if (headerType == HeaderTypeRecovery)
        {
            // Skip to next header
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
        if ((headerFlags & FileFlagComment) != 0)
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
            1 => CompressionType.Ace, // ACE 1.0 LZ77 compression
            2 => CompressionType.Ace2, // ACE 2.0 compression (improved LZ77)
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
