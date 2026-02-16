using System;
using System.Buffers;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using SharpCompress.IO;

namespace SharpCompress.Common.Tar.Headers;

internal sealed partial class TarHeader
{
    internal async ValueTask WriteAsync(
        Stream output,
        CancellationToken cancellationToken = default
    )
    {
        switch (WriteFormat)
        {
            case TarHeaderWriteFormat.GNU_TAR_LONG_LINK:
                await WriteGnuTarLongLinkAsync(output, cancellationToken).ConfigureAwait(false);
                break;
            case TarHeaderWriteFormat.USTAR:
                await WriteUstarAsync(output, cancellationToken).ConfigureAwait(false);
                break;
            default:
                throw new ArchiveOperationException("This should be impossible...");
        }
    }

    private async ValueTask WriteUstarAsync(Stream output, CancellationToken cancellationToken)
    {
        var buffer = new byte[BLOCK_SIZE];

        WriteOctalBytes(511, buffer, 100, 8);
        WriteOctalBytes(0, buffer, 108, 8);
        WriteOctalBytes(0, buffer, 116, 8);

        var nameByteCount = ArchiveEncoding
            .GetEncoding()
            .GetByteCount(Name.NotNull("Name is null"));

        if (nameByteCount > 100)
        {
            string fullName = Name.NotNull("Name is null");

            List<int> dirSeps = new List<int>();
            for (int i = 0; i < fullName.Length; i++)
            {
                if (fullName[i] == Path.DirectorySeparatorChar)
                {
                    dirSeps.Add(i);
                }
            }

            int splitIndex = -1;
            for (int i = 0; i < dirSeps.Count; i++)
            {
#if NET8_0_OR_GREATER
                int count = ArchiveEncoding
                    .GetEncoding()
                    .GetByteCount(fullName.AsSpan(0, dirSeps[i]));
#else
                int count = ArchiveEncoding
                    .GetEncoding()
                    .GetByteCount(fullName.Substring(0, dirSeps[i]));
#endif
                if (count < 155)
                {
                    splitIndex = dirSeps[i];
                }
                else
                {
                    break;
                }
            }

            if (splitIndex == -1)
            {
                throw new InvalidFormatException(
                    $"Tar header USTAR format can not fit file name \"{fullName}\" of length {nameByteCount}! Directory separator not found! Try using GNU Tar format instead!"
                );
            }

            string namePrefix = fullName.Substring(0, splitIndex);
            string name = fullName.Substring(splitIndex + 1);

            if (this.ArchiveEncoding.GetEncoding().GetByteCount(namePrefix) >= 155)
            {
                throw new InvalidFormatException(
                    $"Tar header USTAR format can not fit file name \"{fullName}\" of length {nameByteCount}! Try using GNU Tar format instead!"
                );
            }

            if (this.ArchiveEncoding.GetEncoding().GetByteCount(name) >= 100)
            {
                throw new InvalidFormatException(
                    $"Tar header USTAR format can not fit file name \"{fullName}\" of length {nameByteCount}! Try using GNU Tar format instead!"
                );
            }

            WriteStringBytes(ArchiveEncoding.Encode(namePrefix), buffer, 345, 100);
            WriteStringBytes(ArchiveEncoding.Encode(name), buffer, 100);
        }
        else
        {
            WriteStringBytes(ArchiveEncoding.Encode(Name.NotNull("Name is null")), buffer, 100);
        }

        WriteOctalBytes(Size, buffer, 124, 12);
        var time = (long)(LastModifiedTime.ToUniversalTime() - EPOCH).TotalSeconds;
        WriteOctalBytes(time, buffer, 136, 12);
        buffer[156] = (byte)EntryType;

        WriteStringBytes(Encoding.ASCII.GetBytes("ustar"), buffer, 257, 6);
        buffer[263] = 0x30;
        buffer[264] = 0x30;

        var crc = RecalculateChecksum(buffer);
        WriteOctalBytes(crc, buffer, 148, 8);

        await output.WriteAsync(buffer, 0, buffer.Length, cancellationToken).ConfigureAwait(false);
    }

    private async ValueTask WriteGnuTarLongLinkAsync(
        Stream output,
        CancellationToken cancellationToken
    )
    {
        var buffer = new byte[BLOCK_SIZE];

        WriteOctalBytes(511, buffer, 100, 8);
        WriteOctalBytes(0, buffer, 108, 8);
        WriteOctalBytes(0, buffer, 116, 8);

        var nameByteCount = ArchiveEncoding
            .GetEncoding()
            .GetByteCount(Name.NotNull("Name is null"));
        if (nameByteCount > 100)
        {
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

        await output.WriteAsync(buffer, 0, buffer.Length, cancellationToken).ConfigureAwait(false);

        if (nameByteCount > 100)
        {
            await WriteLongFilenameHeaderAsync(output, cancellationToken).ConfigureAwait(false);
            Name = ArchiveEncoding.Decode(
                ArchiveEncoding.Encode(Name.NotNull("Name is null")),
                0,
                100 - ArchiveEncoding.GetEncoding().GetMaxByteCount(1)
            );
            await WriteGnuTarLongLinkAsync(output, cancellationToken).ConfigureAwait(false);
        }
    }

    private async ValueTask WriteLongFilenameHeaderAsync(
        Stream output,
        CancellationToken cancellationToken
    )
    {
        var nameBytes = ArchiveEncoding.Encode(Name.NotNull("Name is null"));
        await output
            .WriteAsync(nameBytes, 0, nameBytes.Length, cancellationToken)
            .ConfigureAwait(false);

        var numPaddingBytes = BLOCK_SIZE - (nameBytes.Length % BLOCK_SIZE);
        if (numPaddingBytes == 0)
        {
            numPaddingBytes = BLOCK_SIZE;
        }

        await output
            .WriteAsync(new byte[numPaddingBytes], 0, numPaddingBytes, cancellationToken)
            .ConfigureAwait(false);
    }

    internal async ValueTask<bool> ReadAsync(AsyncBinaryReader reader)
    {
        string? longName = null;
        string? longLinkName = null;
        var hasLongValue = true;
        byte[] buffer;
        EntryType entryType;

        do
        {
            buffer = await ReadBlockAsync(reader).ConfigureAwait(false);

            if (buffer.Length == 0)
            {
                return false;
            }

            entryType = ReadEntryType(buffer);

            // LongName and LongLink headers can follow each other and need
            // to apply to the header that follows them.
            if (entryType == EntryType.LongName)
            {
                longName = await ReadLongNameAsync(reader, buffer).ConfigureAwait(false);
                continue;
            }
            else if (entryType == EntryType.LongLink)
            {
                longLinkName = await ReadLongNameAsync(reader, buffer).ConfigureAwait(false);
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

        if (!string.IsNullOrEmpty(Magic) && "ustar".Equals(Magic, StringComparison.Ordinal))
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
            await reader.ReadBytesAsync(buffer, 0, BLOCK_SIZE).ConfigureAwait(false);

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
            await reader.ReadBytesAsync(nameBytes, 0, nameLength).ConfigureAwait(false);
            var remainingBytesToRead = BLOCK_SIZE - (nameLength % BLOCK_SIZE);

            // Read the rest of the block and discard the data
            if (remainingBytesToRead < BLOCK_SIZE)
            {
                var remainingBytes = ArrayPool<byte>.Shared.Rent(remainingBytesToRead);
                try
                {
                    await reader
                        .ReadBytesAsync(remainingBytes, 0, remainingBytesToRead)
                        .ConfigureAwait(false);
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
