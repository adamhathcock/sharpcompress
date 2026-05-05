using System;
using System.Buffers.Binary;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using SharpCompress.Crypto;

namespace SharpCompress.Common.SevenZip;

/// <summary>
/// Handles writing the 7z signature header (32 bytes at position 0 of the archive).
/// Layout: [6 bytes magic] [2 bytes version] [4 bytes StartHeaderCRC] [20 bytes StartHeader]
/// </summary>
internal static class SevenZipSignatureHeaderWriter
{
    /// <summary>
    /// 7z file magic signature bytes.
    /// </summary>
    private static readonly byte[] Signature = [(byte)'7', (byte)'z', 0xBC, 0xAF, 0x27, 0x1C];

    /// <summary>
    /// Total size of the signature header in bytes (6+2+4+8+8+4 = 32).
    /// </summary>
    public const int HeaderSize = 32;

    /// <summary>
    /// Writes a placeholder signature header (all zeros for CRC/offset fields).
    /// Call this at the start of archive creation to reserve space.
    /// </summary>
    public static void WritePlaceholder(Stream stream)
    {
        var header = new byte[HeaderSize];

        // magic signature
        Array.Copy(Signature, 0, header, 0, Signature.Length);

        // version: major=0, minor=2 (standard 7z format)
        header[6] = 0;
        header[7] = 2;

        // remaining 24 bytes are zero (placeholder for CRC and StartHeader)
        stream.Write(header, 0, header.Length);
    }

    /// <summary>
    /// Asynchronously writes a placeholder signature header (all zeros for CRC/offset fields).
    /// Call this at the start of archive creation to reserve space.
    /// </summary>
    public static async Task WritePlaceholderAsync(
        Stream stream,
        CancellationToken cancellationToken = default
    )
    {
        var header = new byte[HeaderSize];

        // magic signature
        Array.Copy(Signature, 0, header, 0, Signature.Length);

        // version: major=0, minor=2 (standard 7z format)
        header[6] = 0;
        header[7] = 2;

        // remaining 24 bytes are zero (placeholder for CRC and StartHeader)
        await stream.WriteAsync(header, 0, header.Length, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Writes the final signature header with correct offsets and CRCs.
    /// The stream must be seekable; this method seeks to position 0.
    /// </summary>
    /// <param name="stream">The archive output stream (seekable).</param>
    /// <param name="nextHeaderOffset">Offset from end of signature header to start of metadata header.</param>
    /// <param name="nextHeaderSize">Size of the metadata header in bytes.</param>
    /// <param name="nextHeaderCrc">CRC32 of the metadata header bytes.</param>
    public static void WriteFinal(
        Stream stream,
        ulong nextHeaderOffset,
        ulong nextHeaderSize,
        uint nextHeaderCrc
    )
    {
        var header = BuildFinalHeader(nextHeaderOffset, nextHeaderSize, nextHeaderCrc);

        // Write at position 0
        stream.Position = 0;
        stream.Write(header, 0, header.Length);
    }

    /// <summary>
    /// Asynchronously writes the final signature header with correct offsets and CRCs.
    /// The stream must be seekable; this method seeks to position 0.
    /// </summary>
    public static async Task WriteFinalAsync(
        Stream stream,
        ulong nextHeaderOffset,
        ulong nextHeaderSize,
        uint nextHeaderCrc,
        CancellationToken cancellationToken = default
    )
    {
        var header = BuildFinalHeader(nextHeaderOffset, nextHeaderSize, nextHeaderCrc);

        // Write at position 0
        stream.Position = 0;
        await stream.WriteAsync(header, 0, header.Length, cancellationToken).ConfigureAwait(false);
    }

    private static byte[] BuildFinalHeader(
        ulong nextHeaderOffset,
        ulong nextHeaderSize,
        uint nextHeaderCrc
    )
    {
        // Build StartHeader (20 bytes): NextHeaderOffset(8) + NextHeaderSize(8) + NextHeaderCRC(4)
        var startHeader = new byte[20];
        BinaryPrimitives.WriteUInt64LittleEndian(startHeader.AsSpan(0, 8), nextHeaderOffset);
        BinaryPrimitives.WriteUInt64LittleEndian(startHeader.AsSpan(8, 8), nextHeaderSize);
        BinaryPrimitives.WriteUInt32LittleEndian(startHeader.AsSpan(16, 4), nextHeaderCrc);

        // CRC32 of StartHeader
        var startHeaderCrc = Crc32Stream.Compute(
            Crc32Stream.DEFAULT_POLYNOMIAL,
            Crc32Stream.DEFAULT_SEED,
            startHeader
        );

        // Assemble full 32-byte header
        var header = new byte[HeaderSize];

        // magic signature
        Array.Copy(Signature, 0, header, 0, Signature.Length);

        // version
        header[6] = 0;
        header[7] = 2;

        // StartHeaderCRC
        BinaryPrimitives.WriteUInt32LittleEndian(header.AsSpan(8, 4), startHeaderCrc);

        // StartHeader
        Array.Copy(startHeader, 0, header, 12, startHeader.Length);

        return header;
    }
}
