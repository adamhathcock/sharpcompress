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
        Span<byte> header = stackalloc byte[HeaderSize];
        header.Clear();

        // magic signature
        Signature.AsSpan().CopyTo(header);

        // version: major=0, minor=2 (standard 7z format)
        header[6] = 0;
        header[7] = 2;

        // remaining 24 bytes are zero (placeholder for CRC and StartHeader)
        stream.Write(header);
    }

    /// <summary>
    /// Asynchronously writes a placeholder signature header (all zeros for CRC/offset fields).
    /// Call this at the start of archive creation to reserve space.
    /// </summary>
    public static async ValueTask WritePlaceholderAsync(
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
        Span<byte> header = stackalloc byte[HeaderSize];
        BuildFinalHeader(header, nextHeaderOffset, nextHeaderSize, nextHeaderCrc);

        // Write at position 0
        stream.Position = 0;
        stream.Write(header);
    }

    /// <summary>
    /// Asynchronously writes the final signature header with correct offsets and CRCs.
    /// The stream must be seekable; this method seeks to position 0.
    /// </summary>
    public static async ValueTask WriteFinalAsync(
        Stream stream,
        ulong nextHeaderOffset,
        ulong nextHeaderSize,
        uint nextHeaderCrc,
        CancellationToken cancellationToken = default
    )
    {
        var header = new byte[HeaderSize];
        BuildFinalHeader(header, nextHeaderOffset, nextHeaderSize, nextHeaderCrc);

        // Write at position 0
        stream.Position = 0;
        await stream.WriteAsync(header, 0, header.Length, cancellationToken).ConfigureAwait(false);
    }

    private static void BuildFinalHeader(
        Span<byte> header,
        ulong nextHeaderOffset,
        ulong nextHeaderSize,
        uint nextHeaderCrc
    )
    {
        // Build StartHeader (20 bytes): NextHeaderOffset(8) + NextHeaderSize(8) + NextHeaderCRC(4)
        var startHeader = header.Slice(12, 20);
        BinaryPrimitives.WriteUInt64LittleEndian(startHeader.Slice(0, 8), nextHeaderOffset);
        BinaryPrimitives.WriteUInt64LittleEndian(startHeader.Slice(8, 8), nextHeaderSize);
        BinaryPrimitives.WriteUInt32LittleEndian(startHeader.Slice(16, 4), nextHeaderCrc);

        // CRC32 of StartHeader
        var startHeaderCrc = Crc32Stream.Compute(
            Crc32Stream.DEFAULT_POLYNOMIAL,
            Crc32Stream.DEFAULT_SEED,
            startHeader
        );

        // Assemble full 32-byte header
        // magic signature
        Signature.AsSpan().CopyTo(header);

        // version
        header[6] = 0;
        header[7] = 2;

        // StartHeaderCRC
        BinaryPrimitives.WriteUInt32LittleEndian(header.Slice(8, 4), startHeaderCrc);
    }
}
