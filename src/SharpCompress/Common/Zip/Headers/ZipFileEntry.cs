using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.IO;

namespace SharpCompress.Common.Zip.Headers;

internal abstract partial class ZipFileEntry(ZipHeaderType type, IArchiveEncoding archiveEncoding)
    : ZipHeader(type)
{
    internal bool IsDirectory
    {
        get
        {
            if (Name?.EndsWith('/') ?? false)
            {
                return true;
            }

            //.NET Framework 4.5 : System.IO.Compression::CreateFromDirectory() probably writes backslashes to headers
            return CompressedSize == 0 && UncompressedSize == 0 && (Name?.EndsWith('\\') ?? false);
        }
    }

    internal Stream? PackedStream { get; set; }

    internal IArchiveEncoding ArchiveEncoding { get; } = archiveEncoding;

    internal string? Name { get; set; }

    internal HeaderFlags Flags { get; set; }

    internal ZipCompressionMethod CompressionMethod { get; set; }

    internal long CompressedSize { get; set; }

    internal long? DataStartPosition { get; set; }

    internal long UncompressedSize { get; set; }

    internal List<ExtraData> Extra { get; set; } = new();

    public string? Password { get; set; }

    internal PkwareTraditionalEncryptionData ComposeEncryptionData(Stream archiveStream)
    {
        ThrowHelper.ThrowIfNull(archiveStream);

        var buffer = new byte[12];
        archiveStream.ReadFully(buffer);

        var encryptionData = PkwareTraditionalEncryptionData.ForRead(Password!, this, buffer);

        return encryptionData;
    }

    internal WinzipAesEncryptionData? WinzipAesEncryptionData { get; set; }

    /// <summary>
    /// The last modified date as read from the Local or Central Directory header.
    /// </summary>
    internal ushort OriginalLastModifiedDate { get; set; }

    /// <summary>
    /// The last modified date from the UnixTimeExtraField, if present, or the
    /// Local or Cental Directory header, if not.
    /// </summary>
    internal ushort LastModifiedDate { get; set; }

    /// <summary>
    /// The last modified time as read from the Local or Central Directory header.
    /// </summary>
    internal ushort OriginalLastModifiedTime { get; set; }

    /// <summary>
    /// The last modified time from the UnixTimeExtraField, if present, or the
    /// Local or Cental Directory header, if not.
    /// </summary>
    internal ushort LastModifiedTime { get; set; }

    internal uint Crc { get; set; }

    protected void LoadExtra(byte[] extra)
    {
        for (var i = 0; i < extra.Length; )
        {
            // Ensure we have at least a header (2-byte ID + 2-byte length)
            if (i + 4 > extra.Length)
            {
                // Incomplete header — stop parsing extras
                break;
            }

            var type = (ExtraDataType)BinaryPrimitives.ReadUInt16LittleEndian(extra.AsSpan(i));
            if (!IsDefined(type))
            {
                type = ExtraDataType.NotImplementedExtraData;
            }

            var length = BinaryPrimitives.ReadUInt16LittleEndian(extra.AsSpan(i + 2));

            // 7zip has this same kind of check to ignore extras blocks that don't conform to the standard 2-byte ID, 2-byte length, N-byte value.
            // CPP/7Zip/Zip/ZipIn.cpp: CInArchive::ReadExtra
            if (length > extra.Length)
            {
                // bad extras block
                break; // allow processing optional other blocks
            }
            // Some ZIP files contain vendor-specific or malformed extra fields where the declared
            // data length extends beyond the remaining buffer. This adjustment ensures that
            // we only read data within bounds (i + 4 + length <= extra.Length)
            // The example here is: 41 43 18 00 41 52 43 30 46 EB FF FF 51 29 03 C6 03 00 00 00 00 00 00 00 00
            // No existing zip utility uses 0x4341 ('AC')
            if (i + 4 + length > extra.Length)
            {
                // incomplete or corrupt field
                break; // allow processing other blocks
            }

            var data = new byte[length];
            Buffer.BlockCopy(extra, i + 4, data, 0, length);
            Extra.Add(LocalEntryHeaderExtraFactory.Create(type, length, data));

            i += length + 4;
        }
    }

    internal ZipFilePart? Part { get; set; }

    internal bool IsZip64 => CompressedSize >= uint.MaxValue;

    internal uint ExternalFileAttributes { get; set; }

    internal string? Comment { get; set; }

    private static bool IsDefined(ExtraDataType type)
    {
#if LEGACY_DOTNET
        return Enum.IsDefined(typeof(ExtraDataType), type);
#else
        return Enum.IsDefined(type);
#endif
    }
}
