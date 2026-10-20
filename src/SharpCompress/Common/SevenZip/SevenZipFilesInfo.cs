using System;
using System.Buffers;
using System.IO;
using System.Text;
using SharpCompress.Compressors.LZMA.Utilities;

namespace SharpCompress.Common.SevenZip;

/// <summary>
/// Entry metadata collected during writing, used to build FilesInfo header.
/// </summary>
internal sealed class SevenZipWriteEntry
{
    public string Name { get; init; } = string.Empty;
    public DateTime? ModificationTime { get; init; }

    // Define attributes for files too: libarchive misreads partially defined vectors.
    public uint? Attributes { get; init; } = 0x20; // FILE_ATTRIBUTE_ARCHIVE
    public bool IsDirectory { get; init; }
    public bool IsEmpty { get; init; }
}

/// <summary>
/// Writes the FilesInfo section of a 7z header, including all file properties
/// (names, timestamps, attributes, empty stream/file markers).
/// </summary>
internal sealed class SevenZipFilesInfoWriter
{
    public SevenZipWriteEntry[] Entries { get; init; } = [];

    public void Write(Stream stream)
    {
        var numFiles = (ulong)Entries.Length;
        stream.WriteEncodedUInt64(numFiles);

        // Count empty streams (directories + zero-length files)
        var emptyStreamCount = 0;
        for (var i = 0; i < Entries.Length; i++)
        {
            if (Entries[i].IsEmpty || Entries[i].IsDirectory)
            {
                emptyStreamCount++;
            }
        }

        // EmptyStream property
        if (emptyStreamCount > 0)
        {
            WriteEmptyStreamProperty(stream, emptyStreamCount);
        }

        // Names property
        WriteNameProperty(stream);

        // MTime property
        WriteMTimeProperty(stream);

        // Attributes property
        WriteAttributesProperty(stream);

        stream.WriteByte((byte)BlockType.End);
    }

    private void WriteEmptyStreamProperty(Stream stream, int emptyStreamCount)
    {
        var hasEmptyFile = false;

        for (var i = 0; i < Entries.Length; i++)
        {
            if (Entries[i].IsEmpty || Entries[i].IsDirectory)
            {
                var isEmptyFile = !Entries[i].IsDirectory;
                if (isEmptyFile)
                {
                    hasEmptyFile = true;
                }
            }
        }

        // kEmptyStream
        WritePropertyHeader(stream, BlockType.EmptyStream, (Entries.LongLength + 7) / 8);
        WriteFlags(stream, BlockType.EmptyStream);

        // kEmptyFile (only if there are actual empty files, not just directories)
        if (hasEmptyFile)
        {
            WritePropertyHeader(stream, BlockType.EmptyFile, ((long)emptyStreamCount + 7) / 8);
            WriteFlags(stream, BlockType.EmptyFile);
        }
    }

    private void WriteNameProperty(Stream stream)
    {
        long size = 1;
        var maxBytes = 0;
        foreach (var entry in Entries)
        {
            var count = Encoding.Unicode.GetByteCount(entry.Name);
            size = checked(size + count + 2);
            maxBytes = Math.Max(maxBytes, count);
        }
        WritePropertyHeader(stream, BlockType.Name, size);
        stream.WriteByte(0);
        byte[]? rented = null;
#if !LEGACY_DOTNET
        Span<byte> small = stackalloc byte[512];
#endif
        try
        {
            foreach (var entry in Entries)
            {
#if !LEGACY_DOTNET
                var count = Encoding.Unicode.GetByteCount(entry.Name);
                if (count <= small.Length)
                {
                    Encoding.Unicode.GetBytes(entry.Name.AsSpan(), small);
                    stream.Write(small.Slice(0, count));
                }
                else
#endif
                {
                    rented ??= ArrayPool<byte>.Shared.Rent(maxBytes);
                    var written = Encoding.Unicode.GetBytes(
                        entry.Name,
                        0,
                        entry.Name.Length,
                        rented,
                        0
                    );
                    stream.Write(rented, 0, written);
                }
                stream.WriteByte(0);
                stream.WriteByte(0);
            }
        }
        finally
        {
            if (rented is not null)
            {
                ArrayPool<byte>.Shared.Return(rented);
            }
        }
    }

    private void WriteMTimeProperty(Stream stream)
    {
        var definedCount = 0;
        for (var i = 0; i < Entries.Length; i++)
        {
            if (Entries[i].ModificationTime is not null)
            {
                definedCount++;
            }
        }

        if (definedCount == 0)
        {
            return;
        }

        WritePropertyHeader(
            stream,
            BlockType.MTime,
            2L
                + (definedCount == Entries.Length ? 0 : (Entries.LongLength + 7) / 8)
                + 8L * definedCount
        );
        stream.WriteByte((byte)(definedCount == Entries.Length ? 1 : 0));
        if (definedCount != Entries.Length)
        {
            WriteFlags(stream, BlockType.MTime);
        }
        stream.WriteByte(0);
        Span<byte> buf = stackalloc byte[8];
        for (var i = 0; i < Entries.Length; i++)
        {
            if (Entries[i].ModificationTime is { } mtime)
            {
                var fileTime = (ulong)mtime.ToUniversalTime().ToFileTimeUtc();
                System.Buffers.Binary.BinaryPrimitives.WriteUInt64LittleEndian(buf, fileTime);
                stream.Write(buf);
            }
        }
    }

    private void WriteAttributesProperty(Stream stream)
    {
        var definedCount = 0;
        for (var i = 0; i < Entries.Length; i++)
        {
            if (Entries[i].Attributes is not null)
            {
                definedCount++;
            }
        }

        if (definedCount == 0)
        {
            return;
        }

        WritePropertyHeader(
            stream,
            BlockType.WinAttributes,
            2L
                + (definedCount == Entries.Length ? 0 : (Entries.LongLength + 7) / 8)
                + 4L * definedCount
        );
        stream.WriteByte((byte)(definedCount == Entries.Length ? 1 : 0));
        if (definedCount != Entries.Length)
        {
            WriteFlags(stream, BlockType.WinAttributes);
        }
        stream.WriteByte(0);
        Span<byte> buf = stackalloc byte[4];
        for (var i = 0; i < Entries.Length; i++)
        {
            if (Entries[i].Attributes is { } attrs)
            {
                System.Buffers.Binary.BinaryPrimitives.WriteUInt32LittleEndian(buf, attrs);
                stream.Write(buf);
            }
        }
    }

    /// <summary>
    /// Writes a property prefix without staging its computable payload in another stream.
    /// </summary>
    private static void WritePropertyHeader(Stream stream, BlockType propertyId, long size)
    {
        stream.WriteByte((byte)propertyId);
        stream.WriteEncodedUInt64((ulong)size);
    }

    private void WriteFlags(Stream stream, BlockType property)
    {
        byte mask = 0x80;
        byte bits = 0;
        foreach (var entry in Entries)
        {
            var empty = entry.IsEmpty || entry.IsDirectory;
            if (property == BlockType.EmptyFile && !empty)
            {
                continue;
            }
            var defined = property switch
            {
                BlockType.EmptyStream => empty,
                BlockType.EmptyFile => !entry.IsDirectory,
                BlockType.MTime => entry.ModificationTime is not null,
                BlockType.WinAttributes => entry.Attributes is not null,
                _ => throw new InvalidOperationException(),
            };
            if (defined)
            {
                bits |= mask;
            }
            mask >>= 1;
            if (mask == 0)
            {
                stream.WriteByte(bits);
                mask = 0x80;
                bits = 0;
            }
        }
        if (mask != 0x80)
        {
            stream.WriteByte(bits);
        }
    }
}
