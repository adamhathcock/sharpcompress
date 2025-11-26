using System;
using System.Buffers.Binary;
using System.IO;

namespace SharpCompress.Common.Zip.SOZip;

/// <summary>
/// Represents a SOZip (Seek-Optimized ZIP) index that enables random access
/// within DEFLATE-compressed files by storing offsets to sync flush points.
/// </summary>
/// <remarks>
/// SOZip index files (.sozip.idx) contain a header followed by offset entries
/// that point to the beginning of independently decompressable DEFLATE blocks.
/// </remarks>
[CLSCompliant(false)]
public sealed class SOZipIndex
{
    /// <summary>
    /// SOZip index file magic number: "SOZo" (0x534F5A6F)
    /// </summary>
    public const uint SOZIP_MAGIC = 0x6F5A4F53; // "SOZo" little-endian

    /// <summary>
    /// Current SOZip specification version
    /// </summary>
    public const byte SOZIP_VERSION = 1;

    /// <summary>
    /// Index file extension suffix
    /// </summary>
    public const string INDEX_EXTENSION = ".sozip.idx";

    /// <summary>
    /// Default chunk size in bytes (32KB)
    /// </summary>
    public const uint DEFAULT_CHUNK_SIZE = 32768;

    /// <summary>
    /// The version of the SOZip index format
    /// </summary>
    public byte Version { get; private set; }

    /// <summary>
    /// Size of each uncompressed chunk in bytes
    /// </summary>
    public uint ChunkSize { get; private set; }

    /// <summary>
    /// Total uncompressed size of the file
    /// </summary>
    public ulong UncompressedSize { get; private set; }

    /// <summary>
    /// Total compressed size of the file
    /// </summary>
    public ulong CompressedSize { get; private set; }

    /// <summary>
    /// Number of offset entries in the index
    /// </summary>
    public uint OffsetCount { get; private set; }

    /// <summary>
    /// Array of compressed offsets for each chunk
    /// </summary>
    public ulong[] CompressedOffsets { get; private set; } = Array.Empty<ulong>();

    /// <summary>
    /// Creates a new empty SOZip index
    /// </summary>
    public SOZipIndex() { }

    /// <summary>
    /// Creates a new SOZip index with specified parameters
    /// </summary>
    /// <param name="chunkSize">Size of each uncompressed chunk</param>
    /// <param name="uncompressedSize">Total uncompressed size</param>
    /// <param name="compressedSize">Total compressed size</param>
    /// <param name="compressedOffsets">Array of compressed offsets</param>
    public SOZipIndex(
        uint chunkSize,
        ulong uncompressedSize,
        ulong compressedSize,
        ulong[] compressedOffsets
    )
    {
        Version = SOZIP_VERSION;
        ChunkSize = chunkSize;
        UncompressedSize = uncompressedSize;
        CompressedSize = compressedSize;
        OffsetCount = (uint)compressedOffsets.Length;
        CompressedOffsets = compressedOffsets;
    }

    /// <summary>
    /// Reads a SOZip index from a stream
    /// </summary>
    /// <param name="stream">The stream containing the index data</param>
    /// <returns>A parsed SOZipIndex instance</returns>
    /// <exception cref="InvalidDataException">If the stream doesn't contain valid SOZip index data</exception>
    public static SOZipIndex Read(Stream stream)
    {
        var index = new SOZipIndex();
        Span<byte> header = stackalloc byte[4];

        // Read magic number
        if (stream.Read(header) != 4)
        {
            throw new InvalidDataException("Invalid SOZip index: unable to read magic number");
        }

        var magic = BinaryPrimitives.ReadUInt32LittleEndian(header);
        if (magic != SOZIP_MAGIC)
        {
            throw new InvalidDataException(
                $"Invalid SOZip index: magic number mismatch (expected 0x{SOZIP_MAGIC:X8}, got 0x{magic:X8})"
            );
        }

        // Read version
        var versionByte = stream.ReadByte();
        if (versionByte < 0)
        {
            throw new InvalidDataException("Invalid SOZip index: unable to read version");
        }
        index.Version = (byte)versionByte;

        if (index.Version != SOZIP_VERSION)
        {
            throw new InvalidDataException(
                $"Unsupported SOZip index version: {index.Version} (expected {SOZIP_VERSION})"
            );
        }

        // Read reserved byte (padding)
        stream.ReadByte();

        // Read chunk size (2 bytes)
        Span<byte> buf2 = stackalloc byte[2];
        if (stream.Read(buf2) != 2)
        {
            throw new InvalidDataException("Invalid SOZip index: unable to read chunk size");
        }

        // Chunk size is stored as (actual_size / 1024) - 1
        var chunkSizeEncoded = BinaryPrimitives.ReadUInt16LittleEndian(buf2);
        index.ChunkSize = ((uint)chunkSizeEncoded + 1) * 1024;

        // Read uncompressed size (8 bytes)
        Span<byte> buf8 = stackalloc byte[8];
        if (stream.Read(buf8) != 8)
        {
            throw new InvalidDataException(
                "Invalid SOZip index: unable to read uncompressed size"
            );
        }
        index.UncompressedSize = BinaryPrimitives.ReadUInt64LittleEndian(buf8);

        // Read compressed size (8 bytes)
        if (stream.Read(buf8) != 8)
        {
            throw new InvalidDataException("Invalid SOZip index: unable to read compressed size");
        }
        index.CompressedSize = BinaryPrimitives.ReadUInt64LittleEndian(buf8);

        // Read offset count (4 bytes)
        if (stream.Read(header) != 4)
        {
            throw new InvalidDataException("Invalid SOZip index: unable to read offset count");
        }
        index.OffsetCount = BinaryPrimitives.ReadUInt32LittleEndian(header);

        // Read offsets
        index.CompressedOffsets = new ulong[index.OffsetCount];
        for (uint i = 0; i < index.OffsetCount; i++)
        {
            if (stream.Read(buf8) != 8)
            {
                throw new InvalidDataException($"Invalid SOZip index: unable to read offset {i}");
            }
            index.CompressedOffsets[i] = BinaryPrimitives.ReadUInt64LittleEndian(buf8);
        }

        return index;
    }

    /// <summary>
    /// Reads a SOZip index from a byte array
    /// </summary>
    /// <param name="data">The byte array containing the index data</param>
    /// <returns>A parsed SOZipIndex instance</returns>
    public static SOZipIndex Read(byte[] data)
    {
        using var stream = new MemoryStream(data);
        return Read(stream);
    }

    /// <summary>
    /// Writes this SOZip index to a stream
    /// </summary>
    /// <param name="stream">The stream to write to</param>
    public void Write(Stream stream)
    {
        Span<byte> buf8 = stackalloc byte[8];

        // Write magic number
        BinaryPrimitives.WriteUInt32LittleEndian(buf8, SOZIP_MAGIC);
        stream.Write(buf8.Slice(0, 4));

        // Write version
        stream.WriteByte(SOZIP_VERSION);

        // Write reserved byte (padding)
        stream.WriteByte(0);

        // Write chunk size (encoded as (size/1024)-1)
        var chunkSizeEncoded = (ushort)((ChunkSize / 1024) - 1);
        BinaryPrimitives.WriteUInt16LittleEndian(buf8, chunkSizeEncoded);
        stream.Write(buf8.Slice(0, 2));

        // Write uncompressed size
        BinaryPrimitives.WriteUInt64LittleEndian(buf8, UncompressedSize);
        stream.Write(buf8);

        // Write compressed size
        BinaryPrimitives.WriteUInt64LittleEndian(buf8, CompressedSize);
        stream.Write(buf8);

        // Write offset count
        BinaryPrimitives.WriteUInt32LittleEndian(buf8, OffsetCount);
        stream.Write(buf8.Slice(0, 4));

        // Write offsets
        foreach (var offset in CompressedOffsets)
        {
            BinaryPrimitives.WriteUInt64LittleEndian(buf8, offset);
            stream.Write(buf8);
        }
    }

    /// <summary>
    /// Converts this SOZip index to a byte array
    /// </summary>
    /// <returns>Byte array containing the serialized index</returns>
    public byte[] ToByteArray()
    {
        using var stream = new MemoryStream();
        Write(stream);
        return stream.ToArray();
    }

    /// <summary>
    /// Gets the index of the chunk that contains the specified uncompressed offset
    /// </summary>
    /// <param name="uncompressedOffset">The uncompressed byte offset</param>
    /// <returns>The chunk index</returns>
    public int GetChunkIndex(long uncompressedOffset)
    {
        if (uncompressedOffset < 0 || (ulong)uncompressedOffset >= UncompressedSize)
        {
            throw new ArgumentOutOfRangeException(
                nameof(uncompressedOffset),
                "Offset is out of range"
            );
        }

        return (int)((ulong)uncompressedOffset / ChunkSize);
    }

    /// <summary>
    /// Gets the compressed offset for the specified chunk index
    /// </summary>
    /// <param name="chunkIndex">The chunk index</param>
    /// <returns>The compressed byte offset for the start of the chunk</returns>
    public ulong GetCompressedOffset(int chunkIndex)
    {
        if (chunkIndex < 0 || chunkIndex >= CompressedOffsets.Length)
        {
            throw new ArgumentOutOfRangeException(
                nameof(chunkIndex),
                "Chunk index is out of range"
            );
        }

        return CompressedOffsets[chunkIndex];
    }

    /// <summary>
    /// Gets the uncompressed offset for the start of the specified chunk
    /// </summary>
    /// <param name="chunkIndex">The chunk index</param>
    /// <returns>The uncompressed byte offset for the start of the chunk</returns>
    public ulong GetUncompressedOffset(int chunkIndex)
    {
        if (chunkIndex < 0 || chunkIndex >= CompressedOffsets.Length)
        {
            throw new ArgumentOutOfRangeException(
                nameof(chunkIndex),
                "Chunk index is out of range"
            );
        }

        return (ulong)chunkIndex * ChunkSize;
    }

    /// <summary>
    /// Gets the name of the SOZip index file for a given entry name
    /// </summary>
    /// <param name="entryName">The main entry name</param>
    /// <returns>The index file name (hidden with .sozip.idx extension)</returns>
    public static string GetIndexFileName(string entryName)
    {
        var directory = Path.GetDirectoryName(entryName);
        var fileName = Path.GetFileName(entryName);

        // The index file is hidden (prefixed with .)
        var indexFileName = $".{fileName}{INDEX_EXTENSION}";

        if (string.IsNullOrEmpty(directory))
        {
            return indexFileName;
        }

        return Path.Combine(directory, indexFileName).Replace('\\', '/');
    }

    /// <summary>
    /// Checks if a file name is a SOZip index file
    /// </summary>
    /// <param name="fileName">The file name to check</param>
    /// <returns>True if the file is a SOZip index file</returns>
    public static bool IsIndexFile(string fileName)
    {
        if (string.IsNullOrEmpty(fileName))
        {
            return false;
        }

        var name = Path.GetFileName(fileName);
        return name.StartsWith(".", StringComparison.Ordinal)
            && name.EndsWith(INDEX_EXTENSION, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Gets the main file name from a SOZip index file name
    /// </summary>
    /// <param name="indexFileName">The index file name</param>
    /// <returns>The main file name, or null if not a valid index file</returns>
    public static string? GetMainFileName(string indexFileName)
    {
        if (!IsIndexFile(indexFileName))
        {
            return null;
        }

        var directory = Path.GetDirectoryName(indexFileName);
        var name = Path.GetFileName(indexFileName);

        // Remove leading '.' and trailing '.sozip.idx'
        var mainName = name.Substring(1, name.Length - 1 - INDEX_EXTENSION.Length);

        if (string.IsNullOrEmpty(directory))
        {
            return mainName;
        }

        return Path.Combine(directory, mainName).Replace('\\', '/');
    }
}
