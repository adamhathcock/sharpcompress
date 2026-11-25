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

        WriteMetadata(buffer);

        var nameByteCount = ArchiveEncoding
            .GetEncoding()
            .GetByteCount(Name.NotNull("Name is null"));

        if (nameByteCount > 100)
        {
            string fullName = Name.NotNull("Name is null");

            // NOTE: tar entry names always use '/', TarWriter normalizes them, so don't use Path.DirectorySeparatorChar
            List<int> dirSeps = new List<int>();
            for (int i = 0; i < fullName.Length; i++)
            {
                if (fullName[i] == '/')
                {
                    dirSeps.Add(i);
                }
            }

            int splitIndex = -1;
            for (int i = 0; i < dirSeps.Count; i++)
            {
#if NET6_0_OR_GREATER
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

        WriteSize(buffer);
        WriteModificationTime(buffer);
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

        WriteMetadata(buffer);

        var name = Name.NotNull("Name is null");
        var longNamePayload = GetGnuLongNamePayload(name);
        if (longNamePayload is not null)
        {
            WriteStringBytes("././@LongLink", buffer, 0, 100);
            buffer[156] = (byte)EntryType.LongName;
            WriteOctalBytes(longNamePayload.Length, buffer, 124, 12);
        }
        else
        {
            WriteStringBytes(ArchiveEncoding.Encode(name), buffer, 100);
            WriteSize(buffer);
            WriteModificationTime(buffer);
            buffer[156] = (byte)EntryType;
        }

        var crc = RecalculateChecksum(buffer);
        WriteOctalBytes(crc, buffer, 148, 8);

        await output.WriteAsync(buffer, 0, buffer.Length, cancellationToken).ConfigureAwait(false);

        if (longNamePayload is not null)
        {
            await WriteLongNamePayloadAsync(output, longNamePayload, cancellationToken)
                .ConfigureAwait(false);
            Name = ArchiveEncoding.Decode(
                ArchiveEncoding.Encode(name),
                0,
                100 - ArchiveEncoding.GetEncoding().GetMaxByteCount(1)
            );
            await WriteGnuTarLongLinkAsync(output, cancellationToken).ConfigureAwait(false);
        }
    }

    private static async ValueTask WriteLongNamePayloadAsync(
        Stream output,
        byte[] payload,
        CancellationToken cancellationToken
    )
    {
        await output
            .WriteAsync(payload, 0, payload.Length, cancellationToken)
            .ConfigureAwait(false);

        var paddingLength = GetPaddingLength(payload.Length);
        if (paddingLength > 0)
        {
            await output
                .WriteAsync(new byte[paddingLength], 0, paddingLength, cancellationToken)
                .ConfigureAwait(false);
        }
    }

    internal ValueTask<bool> ReadAsync(
        AsyncBinaryReader reader,
        PaxMetadata? globalPaxMetadata = null,
        CancellationToken cancellationToken = default
    ) => ReadAsync(reader, globalPaxMetadata, ArrayPool<byte>.Shared, cancellationToken);

    internal async ValueTask<bool> ReadAsync(
        AsyncBinaryReader reader,
        PaxMetadata? globalPaxMetadata,
        ArrayPool<byte> bufferPool,
        CancellationToken cancellationToken = default
    )
    {
        globalPaxMetadata ??= new PaxMetadata();
        var pendingMetadata = globalPaxMetadata.Clone();
        var buffer = bufferPool.Rent(BLOCK_SIZE);
        EntryType entryType;
        try
        {
            while (true)
            {
                await reader
                    .ReadBytesAsync(buffer, 0, BLOCK_SIZE, cancellationToken)
                    .ConfigureAwait(false);
                entryType = ReadEntryType(buffer);

                // LongName and LongLink headers can follow each other and need
                // to apply to the header that follows them.
                if (entryType == EntryType.LongName)
                {
                    pendingMetadata.Name = await ReadLongNameAsync(
                            reader,
                            buffer,
                            cancellationToken
                        )
                        .ConfigureAwait(false);
                    continue;
                }

                if (entryType == EntryType.LongLink)
                {
                    pendingMetadata.LinkName = await ReadLongNameAsync(
                            reader,
                            buffer,
                            cancellationToken
                        )
                        .ConfigureAwait(false);
                    continue;
                }

                if (entryType == EntryType.LocalExtendedHeader)
                {
                    await ReadPaxMetadataAsync(reader, buffer, pendingMetadata, cancellationToken)
                        .ConfigureAwait(false);
                    continue;
                }

                if (entryType == EntryType.GlobalExtendedHeader)
                {
                    await ReadPaxMetadataAsync(reader, buffer, globalPaxMetadata, cancellationToken)
                        .ConfigureAwait(false);
                    pendingMetadata = globalPaxMetadata.Clone();
                    continue;
                }

                break;
            }

            // Check header checksum while the header buffer is still owned by this reader.
            if (!checkChecksum(buffer))
            {
                return false;
            }

            Name = DecodeNullTerminated(buffer, 0, 100);
            EntryType = entryType;
            Size = ReadSize(buffer);
            LinkName = null;

            // for symlinks, additionally read the linkname
            if (entryType == EntryType.SymLink || entryType == EntryType.HardLink)
            {
                LinkName = DecodeNullTerminated(buffer, 157, 100);
            }

            Metadata = new TarEntryMetadata
            {
                Mode = ReadAsciiInt64Base8(buffer, 100, 7),
                UserId = ReadOwnerId(buffer, 108),
                GroupId = ReadOwnerId(buffer, 116),
            };

            LastModifiedTime = ReadModificationTime(buffer);
            Magic = ArchiveEncoding.Decode(buffer, 257, 6).TrimNulls();

            if (!string.IsNullOrEmpty(Magic) && "ustar".Equals(Magic, StringComparison.Ordinal))
            {
                var namePrefix = DecodeNullTerminated(buffer, 345, 157);

                if (!string.IsNullOrEmpty(namePrefix))
                {
                    Name = namePrefix + "/" + Name;
                }
            }
        }
        finally
        {
            bufferPool.Return(buffer);
        }

        pendingMetadata.ApplyTo(this);

        if (entryType != EntryType.LongName && Name.Length == 0)
        {
            return false;
        }

        return true;
    }

    private static async ValueTask ReadLengthAsync(
        AsyncBinaryReader reader,
        int length,
        CancellationToken cancellationToken
    )
    {
        var buffer = ArrayPool<byte>.Shared.Rent(length);
        try
        {
            await reader.ReadBytesAsync(buffer, 0, length, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
        }
    }

    private async ValueTask<string> ReadLongNameAsync(
        AsyncBinaryReader reader,
        byte[] buffer,
        CancellationToken cancellationToken
    )
    {
        var nameBytes = await ReadMetadataPayloadAsync(
                reader,
                buffer,
                MAX_LONG_NAME_SIZE,
                "Long name",
                cancellationToken
            )
            .ConfigureAwait(false);

        return DecodeNullTerminated(nameBytes, 0, nameBytes.Length);
    }

    private async ValueTask ReadPaxMetadataAsync(
        AsyncBinaryReader reader,
        byte[] buffer,
        PaxMetadata pendingMetadata,
        CancellationToken cancellationToken
    )
    {
        var payload = await ReadMetadataPayloadAsync(
                reader,
                buffer,
                MAX_PAX_HEADER_SIZE,
                "PAX header",
                cancellationToken
            )
            .ConfigureAwait(false);

        ParsePaxRecords(payload, pendingMetadata);
    }

    private async ValueTask<byte[]> ReadMetadataPayloadAsync(
        AsyncBinaryReader reader,
        byte[] buffer,
        int maxSize,
        string payloadName,
        CancellationToken cancellationToken
    )
    {
        var size = ReadSize(buffer);

        // Validate size to prevent memory exhaustion from malformed headers
        if (size < 0 || size > maxSize)
        {
            throw new InvalidFormatException(
                $"{payloadName} size {size} is invalid or exceeds maximum allowed size of {maxSize} bytes"
            );
        }

        var payloadLength = (int)size;
        var payload = new byte[payloadLength];
        await reader
            .ReadBytesAsync(payload, 0, payloadLength, cancellationToken)
            .ConfigureAwait(false);

        var paddingLength = GetPaddingLength(payloadLength);
        if (paddingLength > 0)
        {
            await ReadLengthAsync(reader, paddingLength, cancellationToken).ConfigureAwait(false);
        }

        return payload;
    }
}
