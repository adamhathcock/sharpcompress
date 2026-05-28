using System;
using System.IO;
using System.Text;
using SharpCompress.Compressors.LZMA.Utilities;
using SharpCompress.IO;

namespace SharpCompress.Common.SevenZip;

/// <summary>
/// Entry metadata collected during writing, used to build FilesInfo header.
/// </summary>
internal sealed class SevenZipWriteEntry
{
    public string Name { get; init; } = string.Empty;
    public DateTime? ModificationTime { get; init; }
    public uint? Attributes { get; init; }
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
        var emptyStreams = new bool[Entries.Length];
        var emptyFiles = new bool[emptyStreamCount];
        var hasEmptyFile = false;
        var emptyIndex = 0;

        for (var i = 0; i < Entries.Length; i++)
        {
            if (Entries[i].IsEmpty || Entries[i].IsDirectory)
            {
                emptyStreams[i] = true;
                var isEmptyFile = !Entries[i].IsDirectory;
                emptyFiles[emptyIndex++] = isEmptyFile;
                if (isEmptyFile)
                {
                    hasEmptyFile = true;
                }
            }
        }

        // kEmptyStream
        WriteFileProperty(stream, BlockType.EmptyStream, s => s.WriteBoolVector(emptyStreams));

        // kEmptyFile (only if there are actual empty files, not just directories)
        if (hasEmptyFile)
        {
            WriteFileProperty(stream, BlockType.EmptyFile, s => s.WriteBoolVector(emptyFiles));
        }
    }

    private void WriteNameProperty(Stream stream)
    {
        WriteFileProperty(
            stream,
            BlockType.Name,
            s =>
            {
                // External = 0 (inline)
                s.WriteByte(0);

                for (var i = 0; i < Entries.Length; i++)
                {
                    var nameBytes = Encoding.Unicode.GetBytes(Entries[i].Name);
                    s.Write(nameBytes);
                    // null terminator (2 bytes for UTF-16)
                    s.WriteByte(0);
                    s.WriteByte(0);
                }
            }
        );
    }

    private void WriteMTimeProperty(Stream stream)
    {
        var hasTimes = false;
        for (var i = 0; i < Entries.Length; i++)
        {
            if (Entries[i].ModificationTime != null)
            {
                hasTimes = true;
                break;
            }
        }

        if (!hasTimes)
        {
            return;
        }

        WriteFileProperty(
            stream,
            BlockType.MTime,
            s =>
            {
                var defined = new bool[Entries.Length];
                for (var i = 0; i < Entries.Length; i++)
                {
                    defined[i] = Entries[i].ModificationTime != null;
                }
                s.WriteOptionalBoolVector(defined);

                // External = 0 (inline)
                s.WriteByte(0);

                var buf = new byte[8];
                for (var i = 0; i < Entries.Length; i++)
                {
                    if (Entries[i].ModificationTime is { } mtime)
                    {
                        var fileTime = (ulong)mtime.ToUniversalTime().ToFileTimeUtc();
                        System.Buffers.Binary.BinaryPrimitives.WriteUInt64LittleEndian(
                            buf,
                            fileTime
                        );
                        s.Write(buf, 0, 8);
                    }
                }
            }
        );
    }

    private void WriteAttributesProperty(Stream stream)
    {
        var hasAttrs = false;
        for (var i = 0; i < Entries.Length; i++)
        {
            if (Entries[i].Attributes != null)
            {
                hasAttrs = true;
                break;
            }
        }

        if (!hasAttrs)
        {
            return;
        }

        WriteFileProperty(
            stream,
            BlockType.WinAttributes,
            s =>
            {
                var defined = new bool[Entries.Length];
                for (var i = 0; i < Entries.Length; i++)
                {
                    defined[i] = Entries[i].Attributes != null;
                }
                s.WriteOptionalBoolVector(defined);

                // External = 0 (inline)
                s.WriteByte(0);

                var buf = new byte[4];
                for (var i = 0; i < Entries.Length; i++)
                {
                    if (Entries[i].Attributes is { } attrs)
                    {
                        System.Buffers.Binary.BinaryPrimitives.WriteUInt32LittleEndian(buf, attrs);
                        s.Write(buf, 0, 4);
                    }
                }
            }
        );
    }

    /// <summary>
    /// Writes a file property block: PropertyID + size + data.
    /// Size is computed by writing to a temporary buffer first.
    /// </summary>
    private static void WriteFileProperty(
        Stream stream,
        BlockType propertyId,
        Action<Stream> writeData
    )
    {
        using var dataStream = new PooledMemoryStream();
        writeData(dataStream);

        stream.WriteByte((byte)propertyId);
        stream.WriteEncodedUInt64((ulong)dataStream.Length);
        dataStream.Position = 0;
        dataStream.CopyTo(stream);
    }
}
