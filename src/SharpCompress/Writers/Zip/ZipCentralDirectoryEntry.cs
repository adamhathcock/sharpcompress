using System;
using System.Buffers.Binary;
using System.IO;
using System.Text;
using SharpCompress.Common;
using SharpCompress.Common.Zip;
using SharpCompress.Common.Zip.Headers;

namespace SharpCompress.Writers.Zip;

internal class ZipCentralDirectoryEntry
{
    private readonly ZipCompressionMethod compression;
    private readonly string fileName;
    private readonly IArchiveEncoding archiveEncoding;

    public ZipCentralDirectoryEntry(
        ZipCompressionMethod compression,
        string fileName,
        ulong headerOffset,
        IArchiveEncoding archiveEncoding
    )
    {
        this.compression = compression;
        this.fileName = fileName;
        HeaderOffset = headerOffset;
        this.archiveEncoding = archiveEncoding;
    }

    internal DateTime? ModificationTime { get; set; }
    internal string? Comment { get; set; }
    internal uint Crc { get; set; }
    internal ulong Compressed { get; set; }
    internal ulong Decompressed { get; set; }
    internal ushort Zip64HeaderOffset { get; set; }
    internal ulong HeaderOffset { get; }
    internal bool UsesDataDescriptor { get; set; }
    internal bool UsesZip64 { get; set; }

    internal byte[] CreateDataDescriptor()
    {
        if (!UsesZip64 && (Compressed >= uint.MaxValue || Decompressed >= uint.MaxValue))
        {
            throw new NotSupportedException(
                "Attempted to write a stream that is larger than 4GiB without setting the zip64 option"
            );
        }

        // APPNOTE 4.3.9.2: ZIP64 descriptors use 8-byte sizes even for small entries.
        var descriptor = new byte[UsesZip64 ? 24 : 16];
        BinaryPrimitives.WriteUInt32LittleEndian(descriptor, ZipHeaderFactory.POST_DATA_DESCRIPTOR);
        BinaryPrimitives.WriteUInt32LittleEndian(descriptor.AsSpan(4), Crc);
        if (UsesZip64)
        {
            BinaryPrimitives.WriteUInt64LittleEndian(descriptor.AsSpan(8), Compressed);
            BinaryPrimitives.WriteUInt64LittleEndian(descriptor.AsSpan(16), Decompressed);
        }
        else
        {
            BinaryPrimitives.WriteUInt32LittleEndian(descriptor.AsSpan(8), (uint)Compressed);
            BinaryPrimitives.WriteUInt32LittleEndian(descriptor.AsSpan(12), (uint)Decompressed);
        }
        return descriptor;
    }

    internal uint Write(Stream outputStream)
    {
        var encodedFilename = archiveEncoding.Encode(fileName);
        var encodedComment = archiveEncoding.Encode(Comment ?? string.Empty);

        var zip64_stream =
            (UsesZip64 && UsesDataDescriptor)
            || Compressed >= uint.MaxValue
            || Decompressed >= uint.MaxValue;
        var zip64 = zip64_stream || HeaderOffset >= uint.MaxValue;
        var usedCompression = compression;

        var compressedvalue = zip64 ? uint.MaxValue : (uint)Compressed;
        var decompressedvalue = zip64 ? uint.MaxValue : (uint)Decompressed;
        var headeroffsetvalue = zip64 ? uint.MaxValue : (uint)HeaderOffset;
        // The disk number stays zero, so it must not appear in the ZIP64 extra field.
        var extralength = zip64 ? (2 + 2 + 8 + 8 + 8) : 0;

        // Determine version needed to extract:
        // - Version 63 for LZMA, PPMd, BZip2, ZStandard (advanced compression methods)
        // - Version 45 for Zip64 extensions (when enabled or actual sizes require it)
        // - Version 20 for standard Deflate/None compression
        byte version;
        if (
            compression == ZipCompressionMethod.LZMA
            || compression == ZipCompressionMethod.PPMd
            || compression == ZipCompressionMethod.BZip2
            || compression == ZipCompressionMethod.ZStandard
        )
        {
            version = 63;
        }
        else if (zip64 || UsesZip64)
        {
            version = 45;
        }
        else
        {
            version = 20;
        }

        var flags = Equals(archiveEncoding.GetEncoding(), Encoding.UTF8)
            ? HeaderFlags.Efs
            : HeaderFlags.None;
        if (UsesDataDescriptor)
        {
            flags |= HeaderFlags.UsePostDataDescriptor;

            if (usedCompression == ZipCompressionMethod.LZMA)
            {
                flags |= HeaderFlags.Bit1; // eos marker
            }
        }

        // Support for zero byte files
        if (Decompressed == 0 && Compressed == 0)
        {
            usedCompression = ZipCompressionMethod.None;
        }

        Span<byte> intBuf = stackalloc byte[] { 80, 75, 1, 2, version, 0, version, 0 };
        //constant sig, then version made by, then version to extract
        outputStream.Write(intBuf);

        BinaryPrimitives.WriteUInt16LittleEndian(intBuf, (ushort)flags);
        outputStream.Write(intBuf.Slice(0, 2));
        BinaryPrimitives.WriteUInt16LittleEndian(intBuf, (ushort)usedCompression);
        outputStream.Write(intBuf.Slice(0, 2)); // zipping method
        BinaryPrimitives.WriteUInt32LittleEndian(intBuf, ModificationTime.DateTimeToDosTime());
        outputStream.Write(intBuf.Slice(0, 4));

        // zipping date and time
        BinaryPrimitives.WriteUInt32LittleEndian(intBuf, Crc);
        outputStream.Write(intBuf.Slice(0, 4)); // file CRC
        BinaryPrimitives.WriteUInt32LittleEndian(intBuf, compressedvalue);
        outputStream.Write(intBuf.Slice(0, 4)); // compressed file size
        BinaryPrimitives.WriteUInt32LittleEndian(intBuf, decompressedvalue);
        outputStream.Write(intBuf.Slice(0, 4)); // uncompressed file size
        BinaryPrimitives.WriteUInt16LittleEndian(intBuf, (ushort)encodedFilename.Length);
        outputStream.Write(intBuf.Slice(0, 2)); // Filename in zip
        BinaryPrimitives.WriteUInt16LittleEndian(intBuf, (ushort)extralength);
        outputStream.Write(intBuf.Slice(0, 2)); // extra length
        BinaryPrimitives.WriteUInt16LittleEndian(intBuf, (ushort)encodedComment.Length);
        outputStream.Write(intBuf.Slice(0, 2));

        BinaryPrimitives.WriteUInt16LittleEndian(intBuf, 0);
        outputStream.Write(intBuf.Slice(0, 2)); // disk=0

        // Internal file attributes:
        // Bit 0: apparent ASCII/ text file
        // Bit 1: reserved
        // Bit 2: control field records precede logical records
        // Bits 3 - 16: unused
        BinaryPrimitives.WriteUInt16LittleEndian(intBuf, 0);
        outputStream.Write(intBuf.Slice(0, 2)); // file type: binary, Internal file attributes

        // External flags are host-dependent, this might match DOS
        // Bit 0: Read-Only
        // Bit 1: Hidden
        // Bit 2: System
        // Bit 3: Label
        // Bit 4: Directory
        // Bit 5: Archive
        BinaryPrimitives.WriteUInt16LittleEndian(intBuf, 0);
        outputStream.Write(intBuf.Slice(0, 2)); // External file attributes
        BinaryPrimitives.WriteUInt16LittleEndian(intBuf, 0x8100);
        outputStream.Write(intBuf.Slice(0, 2));

        // External file attributes (normal/readable)
        BinaryPrimitives.WriteUInt32LittleEndian(intBuf, headeroffsetvalue);
        outputStream.Write(intBuf.Slice(0, 4)); // Offset of header

        outputStream.Write(encodedFilename, 0, encodedFilename.Length);
        if (zip64)
        {
            BinaryPrimitives.WriteUInt16LittleEndian(intBuf, 0x0001);
            outputStream.Write(intBuf.Slice(0, 2));
            BinaryPrimitives.WriteUInt16LittleEndian(intBuf, (ushort)(extralength - 4));
            outputStream.Write(intBuf.Slice(0, 2));

            BinaryPrimitives.WriteUInt64LittleEndian(intBuf, Decompressed);
            outputStream.Write(intBuf);
            BinaryPrimitives.WriteUInt64LittleEndian(intBuf, Compressed);
            outputStream.Write(intBuf);
            BinaryPrimitives.WriteUInt64LittleEndian(intBuf, HeaderOffset);
            outputStream.Write(intBuf);
        }

        outputStream.Write(encodedComment, 0, encodedComment.Length);

        return (uint)(
            8
            + 2
            + 2
            + 4
            + 4
            + 4
            + 4
            + 2
            + 2
            + 2
            + 2
            + 2
            + 2
            + 2
            + 4
            + encodedFilename.Length
            + extralength
            + encodedComment.Length
        );
    }
}
