using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
#if !LEGACY_DOTNET
using Microsoft.Win32.SafeHandles;
#endif

namespace SharpCompress.Compressors.LZMA;

/// <summary>
/// A contiguous run of one or more independent LZMA2 chunks that can be decoded without any
/// state (dictionary / range-coder) carried over from a preceding chunk.
/// </summary>
internal readonly record struct Lzma2Block(
    long PackOffset,
    long PackLen,
    long UnpackOffset,
    long UnpackLen
);

/// <summary>
/// Splits a single LZMA2-coded stream into independently-decodable blocks and decodes them
/// concurrently. The block-splitting strategy mirrors 7-Zip's own multi-threaded LZMA2 decoder
/// (see the reference 7-Zip C source, Lzma2DecMt.c / MtDec.c): independent restart points
/// (LZMA2 control byte 0x01, or 0xE0-0xFF) are candidate block boundaries, but consecutive
/// restart segments are merged together until the accumulated block reaches at least
/// <see cref="MinBlockSize"/> decoded bytes -- 7-Zip's own comment for this is "we decode small
/// blocks in one thread" -- to avoid parallelization overhead for tiny fragments. Merging also
/// stops once a block would exceed <see cref="MaxBlockSize"/>, matching 7-Zip's outBlockMax
/// default (Lzma2DecMtProps_Init: 1&lt;&lt;28 / 256MB).
/// </summary>
internal static class Lzma2ParallelDecoder
{
    // 7-Zip Lzma2DecMt.c (block-parse loop): "we decode small blocks in one thread" --
    // if (t->dec.decoder.dicPos >= (1 << 14)) break;
    internal const int MinBlockSize = 1 << 14;

    // 7-Zip Lzma2DecMt.c: Lzma2DecMtProps_Init -> p->outBlockMax = 1 << 28
    internal const long MaxBlockSize = 1 << 28;

    // 7-Zip MtDec.h: #define MTDEC_THREADS_MAX 32
    internal const int MaxThreads = 32;

    /// <summary>
    /// Header-only walk of the LZMA2 chunk stream (no LZMA decoding) that locates every
    /// independent restart point and merges adjacent segments into blocks using the same size
    /// thresholds 7-Zip's own MT decoder uses. Returns null if the stream doesn't parse as
    /// well-formed LZMA2 or the parsed total doesn't match <paramref name="unpackSize"/>; callers
    /// should fall back to ordinary sequential decoding in that case.
    /// </summary>
    internal static List<Lzma2Block>? TryScanBlocks(
        Stream packStream,
        long packStart,
        long packSize,
        long unpackSize
    )
    {
        try
        {
            packStream.Position = packStart;

            var blocks = new List<Lzma2Block>();
            long packPos = 0;
            long unpackPos = 0;

            var blockPackStart = 0L;
            var blockUnpackStart = 0L;
            var blockUnpackSize = 0L;
            var blockHasContent = false;

            Span<byte> sizeBuf = stackalloc byte[2];
            Span<byte> header = stackalloc byte[4];

            while (packPos < packSize)
            {
                var chunkPackStart = packPos;
                var control = packStream.ReadByte();
                if (control < 0)
                {
                    return null; // truncated stream
                }
                packPos++;

                if (control == 0)
                {
                    break; // end-of-stream marker
                }

                long chunkUnpackSize;
                long chunkPackPayload;
                bool isRestart;

                if (control < 0x80)
                {
                    // 0x01 = uncompressed chunk + dictionary reset, 0x02 = uncompressed, no reset
                    if (control > 2)
                    {
                        return null; // not a recognized LZMA2 control byte
                    }
                    if (!ReadFully(packStream, sizeBuf))
                    {
                        return null;
                    }
                    packPos += 2;
                    chunkUnpackSize = ((sizeBuf[0] << 8) | sizeBuf[1]) + 1;
                    chunkPackPayload = chunkUnpackSize;
                    isRestart = control == 1;
                }
                else
                {
                    // 0x80-0xFF = compressed chunk; bit layout: 1uuuuu, u = high bits of unpack size
                    if (!ReadFully(packStream, header))
                    {
                        return null;
                    }
                    packPos += 4;
                    chunkUnpackSize =
                        (long)(((control & 0x1F) << 16) | (header[0] << 8) | header[1]) + 1;
                    chunkPackPayload = ((header[2] << 8) | header[3]) + 1;
                    // 0xA0-0xBF state reset, 0xC0-0xDF state reset + new props, 0xE0-0xFF also
                    // resets the dictionary -- only a dictionary reset is an independent restart.
                    isRestart = control >= 0xE0;

                    // 0xC0-0xFF (new-props flag, bits 6-5 = 1x) carries one extra properties byte
                    // right after the 4 size-field bytes, before the compressed payload begins.
                    if (control >= 0xC0)
                    {
                        if (packStream.ReadByte() < 0)
                        {
                            return null;
                        }
                        packPos++;
                    }
                }

                if (
                    isRestart
                    && blockHasContent
                    && (
                        blockUnpackSize >= MinBlockSize
                        || blockUnpackSize + chunkUnpackSize > MaxBlockSize
                    )
                )
                {
                    blocks.Add(
                        new Lzma2Block(
                            blockPackStart,
                            chunkPackStart - blockPackStart,
                            blockUnpackStart,
                            blockUnpackSize
                        )
                    );
                    blockPackStart = chunkPackStart;
                    blockUnpackStart = unpackPos;
                    blockUnpackSize = 0;
                    blockHasContent = false;
                }

                packPos += chunkPackPayload;
                if (packPos > packSize)
                {
                    return null; // chunk claims more pack bytes than the folder actually has
                }
                packStream.Seek(chunkPackPayload, SeekOrigin.Current);
                unpackPos += chunkUnpackSize;
                blockUnpackSize += chunkUnpackSize;
                blockHasContent = true;
            }

            if (unpackPos != unpackSize || packPos != packSize)
            {
                return null; // scan didn't land on the expected totals -- don't trust it
            }

            if (blockHasContent)
            {
                blocks.Add(
                    new Lzma2Block(
                        blockPackStart,
                        packPos - blockPackStart,
                        blockUnpackStart,
                        blockUnpackSize
                    )
                );
            }

            // Guards against a single unmerged segment large enough to overflow a byte[] buffer
            // (only possible with an extreme, effectively-pathological run with zero restarts).
            foreach (var block in blocks)
            {
                if (block.PackLen > int.MaxValue - 16 || block.UnpackLen > int.MaxValue - 16)
                {
                    return null;
                }
            }

            return blocks;
        }
        catch (Exception)
        {
            return null;
        }
    }

    private static bool ReadFully(Stream stream, Span<byte> buffer)
    {
        var total = 0;
        while (total < buffer.Length)
        {
            var read = stream.Read(buffer.Slice(total));
            if (read <= 0)
            {
                return false;
            }
            total += read;
        }
        return true;
    }

#if !LEGACY_DOTNET
    /// <summary>
    /// Decodes every block concurrently, reading packed bytes positionally from
    /// <paramref name="inputHandle"/> and writing decoded bytes positionally to
    /// <paramref name="outputHandle"/>. Both handles must support positional (random) access
    /// since blocks complete out of order across threads.
    /// </summary>
    internal static void DecodeBlocksParallel(
        SafeFileHandle inputHandle,
        byte[] lzma2Props,
        long packStart,
        IReadOnlyList<Lzma2Block> blocks,
        SafeFileHandle outputHandle,
        int maxDegreeOfParallelism
    )
    {
        Parallel.ForEach(
            blocks,
            new ParallelOptions { MaxDegreeOfParallelism = maxDegreeOfParallelism },
            block =>
            {
                var packBuffer = new byte[block.PackLen];
                ReadFullyAt(inputHandle, packBuffer, packStart + block.PackOffset);

                using var packStream = new MemoryStream(packBuffer, writable: false);
                using var lzma = LzmaStream.Create(
                    lzma2Props,
                    packStream,
                    -1,
                    block.UnpackLen,
                    leaveOpen: true
                );

                var buffer = new byte[Math.Min(block.UnpackLen, 1 << 20)];
                long total = 0;
                while (total < block.UnpackLen)
                {
                    var toRead = (int)Math.Min(buffer.Length, block.UnpackLen - total);
                    var read = lzma.Read(buffer, 0, toRead);
                    if (read <= 0)
                    {
                        throw new EndOfStreamException(
                            $"LZMA2 block at unpack offset {block.UnpackOffset:N0} ended early after {total:N0}/{block.UnpackLen:N0} bytes."
                        );
                    }
                    RandomAccess.Write(
                        outputHandle,
                        buffer.AsSpan(0, read),
                        block.UnpackOffset + total
                    );
                    total += read;
                }
            }
        );
    }

    private static void ReadFullyAt(SafeFileHandle handle, byte[] buffer, long fileOffset)
    {
        var total = 0;
        while (total < buffer.Length)
        {
            var read = RandomAccess.Read(handle, buffer.AsSpan(total), fileOffset + total);
            if (read <= 0)
            {
                throw new EndOfStreamException(
                    "Unexpected end of pack stream while reading an LZMA2 block."
                );
            }
            total += read;
        }
    }
#endif
}
