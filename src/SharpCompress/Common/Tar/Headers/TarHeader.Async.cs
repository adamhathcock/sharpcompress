using System;
using System.Buffers;
using System.IO;
using System.Threading.Tasks;

namespace SharpCompress.Common.Tar.Headers;

internal sealed partial class TarHeader
{
    internal async ValueTask<bool> ReadAsync(AsyncBinaryReader reader)
    {
        string? longName = null;
        string? longLinkName = null;
        var hasLongValue = true;
        byte[] buffer;
        EntryType entryType;

        do
        {
            buffer = await ReadBlockAsync(reader);

            if (buffer.Length == 0)
            {
                return false;
            }

            entryType = ReadEntryType(buffer);

            // LongName and LongLink headers can follow each other and need
            // to apply to the header that follows them.
            if (entryType == EntryType.LongName)
            {
                longName = await ReadLongNameAsync(reader, buffer);
                continue;
            }
            else if (entryType == EntryType.LongLink)
            {
                longLinkName = await ReadLongNameAsync(reader, buffer);
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

    private static async ValueTask<byte[]> ReadBlockAsync(AsyncBinaryReader reader)
    {
        var buffer = ArrayPool<byte>.Shared.Rent(BLOCK_SIZE);
        try
        {
            await reader.ReadBytesAsync(buffer, 0, BLOCK_SIZE);

            if (buffer.Length != 0 && buffer.Length < BLOCK_SIZE)
            {
                throw new InvalidFormatException("Buffer is invalid size");
            }

            return buffer;
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
        }
    }

    private async ValueTask<string> ReadLongNameAsync(AsyncBinaryReader reader, byte[] buffer)
    {
        var size = ReadSize(buffer);

        // Validate size to prevent memory exhaustion from malformed headers
        if (size < 0 || size > MAX_LONG_NAME_SIZE)
        {
            throw new InvalidFormatException(
                $"Long name size {size} is invalid or exceeds maximum allowed size of {MAX_LONG_NAME_SIZE} bytes"
            );
        }

        var nameLength = (int)size;
        var nameBytes = ArrayPool<byte>.Shared.Rent(nameLength);
        try
        {
            await reader.ReadBytesAsync(nameBytes, 0, nameLength);
            var remainingBytesToRead = BLOCK_SIZE - (nameLength % BLOCK_SIZE);

            // Read the rest of the block and discard the data
            if (remainingBytesToRead < BLOCK_SIZE)
            {
                var remainingBytes = ArrayPool<byte>.Shared.Rent(remainingBytesToRead);
                try
                {
                    await reader.ReadBytesAsync(remainingBytes, 0, remainingBytesToRead);
                }
                finally
                {
                    ArrayPool<byte>.Shared.Return(remainingBytes);
                }
            }

            return ArchiveEncoding.Decode(nameBytes, 0, nameLength).TrimNulls();
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(nameBytes);
        }
    }
}
