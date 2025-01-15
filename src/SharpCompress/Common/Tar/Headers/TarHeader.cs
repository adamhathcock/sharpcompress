using System;
using System.Buffers.Binary;
using System.IO;
using System.Text;

namespace SharpCompress.Common.Tar.Headers;

internal sealed class TarHeader
{
    internal static readonly DateTime EPOCH = new(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    public TarHeader(ArchiveEncoding archiveEncoding) => ArchiveEncoding = archiveEncoding;

    internal string? Name { get; set; }
    internal string? LinkName { get; set; }

    internal long Mode { get; set; }
    internal long UserId { get; set; }
    internal long GroupId { get; set; }
    internal long Size { get; set; }
    internal DateTime LastModifiedTime { get; set; }
    internal EntryType EntryType { get; set; }
    internal Stream? PackedStream { get; set; }
    internal ArchiveEncoding ArchiveEncoding { get; }

    internal const int BLOCK_SIZE = 512;

    internal void Write(Stream output)
    {
        var buffer = new byte[BLOCK_SIZE];

        WriteOctalBytes(511, buffer, 100, 8); // file mode
        WriteOctalBytes(0, buffer, 108, 8); // owner ID
        WriteOctalBytes(0, buffer, 116, 8); // group ID

        //ArchiveEncoding.UTF8.GetBytes("magic").CopyTo(buffer, 257);
        var nameByteCount = ArchiveEncoding
            .GetEncoding()
            .GetByteCount(Name.NotNull("Name is null"));
        if (nameByteCount > 100)
        {
            // Set mock filename and filetype to indicate the next block is the actual name of the file
            WriteStringBytes("././@LongLink", buffer, 0, 100);
            buffer[156] = (byte)EntryType.LongName;
            WriteOctalBytes(nameByteCount + 1, buffer, 124, 12);
        }
        else
        {
            WriteStringBytes(ArchiveEncoding.Encode(Name.NotNull("Name is null")), buffer, 100);
            WriteOctalBytes(Size, buffer, 124, 12);
            var time = (long)(LastModifiedTime.ToUniversalTime() - EPOCH).TotalSeconds;
            WriteOctalBytes(time, buffer, 136, 12);
            buffer[156] = (byte)EntryType;

            if (Size >= 0x1FFFFFFFF)
            {
                Span<byte> bytes12 = stackalloc byte[12];
                BinaryPrimitives.WriteInt64BigEndian(bytes12.Slice(4), Size);
                bytes12[0] |= 0x80;
                bytes12.CopyTo(buffer.AsSpan(124));
            }
        }

        var crc = RecalculateChecksum(buffer);
        WriteOctalBytes(crc, buffer, 148, 8);

        output.Write(buffer, 0, buffer.Length);

        if (nameByteCount > 100)
        {
            WriteLongFilenameHeader(output);
            // update to short name lower than 100 - [max bytes of one character].
            // subtracting bytes is needed because preventing infinite loop(example code is here).
            //
            // var bytes = Encoding.UTF8.GetBytes(new string(0x3042, 100));
            // var truncated = Encoding.UTF8.GetBytes(Encoding.UTF8.GetString(bytes, 0, 100));
            //
            // and then infinite recursion is occured in WriteLongFilenameHeader because truncated.Length is 102.
            Name = ArchiveEncoding.Decode(
                ArchiveEncoding.Encode(Name.NotNull("Name is null")),
                0,
                100 - ArchiveEncoding.GetEncoding().GetMaxByteCount(1)
            );
            Write(output);
        }
    }

    private void WriteLongFilenameHeader(Stream output)
    {
        var nameBytes = ArchiveEncoding.Encode(Name.NotNull("Name is null"));
        output.Write(nameBytes, 0, nameBytes.Length);

        // pad to multiple of BlockSize bytes, and make sure a terminating null is added
        var numPaddingBytes = BLOCK_SIZE - (nameBytes.Length % BLOCK_SIZE);
        if (numPaddingBytes == 0)
        {
            numPaddingBytes = BLOCK_SIZE;
        }
        output.Write(stackalloc byte[numPaddingBytes]);
    }

    internal bool Read(BinaryReader reader)
    {
        string? longName = null;
        string? longLinkName = null;
        var hasLongValue = true;
        byte[] buffer;
        EntryType entryType;

        do
        {
            buffer = ReadBlock(reader);

            if (buffer.Length == 0)
            {
                return false;
            }

            entryType = ReadEntryType(buffer);

            // LongName and LongLink headers can follow each other and need
            // to apply to the header that follows them.
            if (entryType == EntryType.LongName)
            {
                longName = ReadLongName(reader, buffer);
                continue;
            }
            else if (entryType == EntryType.LongLink)
            {
                longLinkName = ReadLongName(reader, buffer);
                continue;
            }

            hasLongValue = false;
        } while (hasLongValue);

        // Check header checksum
        if (!checkChecksum(buffer))
        {
            return false;
        }

        Name = longName ?? ArchiveEncoding.Decode(buffer, 0, 100).TrimNulls();
        EntryType = entryType;
        Size = ReadSize(buffer);

        // for symlinks, additionally read the linkname
        if (entryType == EntryType.SymLink || entryType == EntryType.HardLink)
        {
            LinkName = longLinkName ?? ArchiveEncoding.Decode(buffer, 157, 100).TrimNulls();
        }

        Mode = ReadAsciiInt64Base8(buffer, 100, 7);

        if (entryType == EntryType.Directory)
        {
            Mode |= 0b1_000_000_000;
        }

        UserId = ReadAsciiInt64Base8oldGnu(buffer, 108, 7);
        GroupId = ReadAsciiInt64Base8oldGnu(buffer, 116, 7);

        var unixTimeStamp = ReadAsciiInt64Base8(buffer, 136, 11);

        LastModifiedTime = EPOCH.AddSeconds(unixTimeStamp).ToLocalTime();
        Magic = ArchiveEncoding.Decode(buffer, 257, 6).TrimNulls();

        if (!string.IsNullOrEmpty(Magic) && "ustar".Equals(Magic))
        {
            var namePrefix = ArchiveEncoding.Decode(buffer, 345, 157).TrimNulls();

            if (!string.IsNullOrEmpty(namePrefix))
            {
                Name = namePrefix + "/" + Name;
            }
        }

        if (entryType != EntryType.LongName && Name.Length == 0)
        {
            return false;
        }

        return true;
    }

    private string ReadLongName(BinaryReader reader, byte[] buffer)
    {
        var size = ReadSize(buffer);
        var nameLength = (int)size;
        var nameBytes = reader.ReadBytes(nameLength);
        var remainingBytesToRead = BLOCK_SIZE - (nameLength % BLOCK_SIZE);

        // Read the rest of the block and discard the data
        if (remainingBytesToRead < BLOCK_SIZE)
        {
            reader.ReadBytes(remainingBytesToRead);
        }
        return ArchiveEncoding.Decode(nameBytes, 0, nameBytes.Length).TrimNulls();
    }

    private static EntryType ReadEntryType(byte[] buffer) => (EntryType)buffer[156];

    private long ReadSize(byte[] buffer)
    {
        if ((buffer[124] & 0x80) == 0x80) // if size in binary
        {
            return BinaryPrimitives.ReadInt64BigEndian(buffer.AsSpan(0x80));
        }

        return ReadAsciiInt64Base8(buffer, 124, 11);
    }

    private static byte[] ReadBlock(BinaryReader reader)
    {
        var buffer = reader.ReadBytes(BLOCK_SIZE);

        if (buffer.Length != 0 && buffer.Length < BLOCK_SIZE)
        {
            throw new InvalidOperationException("Buffer is invalid size");
        }
        return buffer;
    }

    private static void WriteStringBytes(ReadOnlySpan<byte> name, Span<byte> buffer, int length)
    {
        name.CopyTo(buffer);
        var i = Math.Min(length, name.Length);
        buffer.Slice(i, length - i).Clear();
    }

    private static void WriteStringBytes(string name, byte[] buffer, int offset, int length)
    {
        int i;

        for (i = 0; i < length && i < name.Length; ++i)
        {
            buffer[offset + i] = (byte)name[i];
        }

        for (; i < length; ++i)
        {
            buffer[offset + i] = 0;
        }
    }

    private static void WriteOctalBytes(long value, byte[] buffer, int offset, int length)
    {
        var val = Convert.ToString(value, 8);
        var shift = length - val.Length - 1;
        for (var i = 0; i < shift; i++)
        {
            buffer[offset + i] = (byte)' ';
        }
        for (var i = 0; i < val.Length; i++)
        {
            buffer[offset + i + shift] = (byte)val[i];
        }
    }

    private static int ReadAsciiInt32Base8(byte[] buffer, int offset, int count)
    {
        var s = Encoding.UTF8.GetString(buffer, offset, count).TrimNulls();
        if (string.IsNullOrEmpty(s))
        {
            return 0;
        }
        return Convert.ToInt32(s, 8);
    }

    private static long ReadAsciiInt64Base8(byte[] buffer, int offset, int count)
    {
        var s = Encoding.UTF8.GetString(buffer, offset, count).TrimNulls();
        if (string.IsNullOrEmpty(s))
        {
            return 0;
        }
        return Convert.ToInt64(s, 8);
    }

    private static long ReadAsciiInt64Base8oldGnu(byte[] buffer, int offset, int count)
    {
        if (buffer[offset] == 0x80 && buffer[offset + 1] == 0x00)
        {
            return buffer[offset + 4] << 24
                | buffer[offset + 5] << 16
                | buffer[offset + 6] << 8
                | buffer[offset + 7];
        }
        var s = Encoding.UTF8.GetString(buffer, offset, count).TrimNulls();

        if (string.IsNullOrEmpty(s))
        {
            return 0;
        }
        return Convert.ToInt64(s, 8);
    }

    private static long ReadAsciiInt64(byte[] buffer, int offset, int count)
    {
        var s = Encoding.UTF8.GetString(buffer, offset, count).TrimNulls();
        if (string.IsNullOrEmpty(s))
        {
            return 0;
        }
        return Convert.ToInt64(s);
    }

    private static readonly byte[] eightSpaces =
    {
        (byte)' ',
        (byte)' ',
        (byte)' ',
        (byte)' ',
        (byte)' ',
        (byte)' ',
        (byte)' ',
        (byte)' ',
    };

    internal static bool checkChecksum(byte[] buf)
    {
        const int eightSpacesChksum = 256;
        var buffer = new Span<byte>(buf).Slice(0, 512);
        int posix_sum = eightSpacesChksum;
        int sun_sum = eightSpacesChksum;

        foreach (byte b in buffer)
        {
            posix_sum += b;
            sun_sum += unchecked((sbyte)b);
        }

        // Special case, empty file header
        if (posix_sum == eightSpacesChksum)
        {
            return true;
        }

        // Remove current checksum from calculation
        foreach (byte b in buffer.Slice(148, 8))
        {
            posix_sum -= b;
            sun_sum -= unchecked((sbyte)b);
        }

        // Read and compare checksum for header
        var crc = ReadAsciiInt64Base8(buf, 148, 7);
        if (crc != posix_sum && crc != sun_sum)
        {
            return false;
        }

        return true;
    }

    internal static int RecalculateChecksum(byte[] buf)
    {
        // Set default value for checksum. That is 8 spaces.
        eightSpaces.CopyTo(buf, 148);

        // Calculate checksum
        var headerChecksum = 0;
        foreach (var b in buf)
        {
            headerChecksum += b;
        }
        return headerChecksum;
    }

    internal static int RecalculateAltChecksum(byte[] buf)
    {
        eightSpaces.CopyTo(buf, 148);
        var headerChecksum = 0;
        foreach (var b in buf)
        {
            if ((b & 0x80) == 0x80)
            {
                headerChecksum -= b ^ 0x80;
            }
            else
            {
                headerChecksum += b;
            }
        }
        return headerChecksum;
    }

    public long? DataStartPosition { get; set; }

    public string? Magic { get; set; }
}
