using System;
using System.Buffers;
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

    // Bound each worker's transient buffers independently of the compressed block size.
    internal const int BlockInputBufferSize = 1 << 16;
    internal const int BlockOutputBufferSize = 1 << 20;

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
    ) =>
        DecodeBlocksParallel(
            inputHandle,
            lzma2Props,
            packStart,
            blocks,
            outputHandle,
            maxDegreeOfParallelism,
            ArrayPool<byte>.Shared
        );

    internal static void DecodeBlocksParallel(
        SafeFileHandle inputHandle,
        byte[] lzma2Props,
        long packStart,
        IReadOnlyList<Lzma2Block> blocks,
        SafeFileHandle outputHandle,
        int maxDegreeOfParallelism,
        ArrayPool<byte> bufferPool
    )
    {
        ThrowHelper.ThrowIfNull(bufferPool);

        Parallel.ForEach(
            blocks,
            new ParallelOptions { MaxDegreeOfParallelism = maxDegreeOfParallelism },
            block =>
            {
                using var packStream = new RandomAccessBlockStream(
                    inputHandle,
                    checked(packStart + block.PackOffset),
                    block.PackLen,
                    bufferPool
                );
                using var lzma = LzmaStream.Create(
                    lzma2Props,
                    packStream,
                    block.PackLen,
                    block.UnpackLen,
                    leaveOpen: true
                );

                var bufferSize = checked(
                    (int)Math.Min(Math.Max(block.UnpackLen, 1), BlockOutputBufferSize)
                );
                var buffer = bufferPool.Rent(bufferSize);
                try
                {
                    long total = 0;
                    while (total < block.UnpackLen)
                    {
                        var toRead = (int)Math.Min(bufferSize, block.UnpackLen - total);
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
                            checked(block.UnpackOffset + total)
                        );
                        total += read;
                    }
                }
                finally
                {
                    bufferPool.Return(buffer, clearArray: true);
                }
            }
        );
    }

    private sealed class RandomAccessBlockStream : Stream
    {
        private readonly SafeFileHandle _inputHandle;
        private readonly long _start;
        private readonly long _length;
        private readonly ArrayPool<byte> _bufferPool;
        private byte[]? _buffer;
        private long _bufferStart = -1;
        private int _bufferLength;
        private long _position;

        internal RandomAccessBlockStream(
            SafeFileHandle inputHandle,
            long start,
            long length,
            ArrayPool<byte> bufferPool
        )
        {
            ThrowHelper.ThrowIfNegative(length);

            _inputHandle = inputHandle;
            _start = start;
            _length = length;
            _bufferPool = bufferPool;
            _buffer = bufferPool.Rent(BlockInputBufferSize);
        }

        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => _length;

        public override long Position
        {
            get => _position;
            set => throw new NotSupportedException();
        }

        public override void Flush() { }

        public override int Read(byte[] buffer, int offset, int count) =>
            Read(buffer.AsSpan(offset, count));

        public override int Read(Span<byte> buffer)
        {
            var total = 0;
            while (!buffer.IsEmpty && _position < _length)
            {
                if (!FillBuffer())
                {
                    break;
                }
                var bufferOffset = checked((int)(_position - _bufferStart));
                var count = Math.Min(_bufferLength - bufferOffset, buffer.Length);
                _buffer!.AsSpan(bufferOffset, count).CopyTo(buffer);
                _position += count;
                total += count;
                buffer = buffer.Slice(count);
            }
            return total;
        }

        public override int ReadByte()
        {
            if (_position >= _length)
            {
                return -1;
            }

            if (!FillBuffer())
            {
                return -1;
            }
            var value = _buffer![checked((int)(_position - _bufferStart))];
            _position++;
            return value;
        }

        public override long Seek(long offset, SeekOrigin origin) =>
            throw new NotSupportedException();

        public override void SetLength(long value) => throw new NotSupportedException();

        public override void Write(byte[] buffer, int offset, int count) =>
            throw new NotSupportedException();

        protected override void Dispose(bool disposing)
        {
            if (disposing && _buffer is not null)
            {
                _bufferPool.Return(_buffer, clearArray: true);
                _buffer = null;
            }

            base.Dispose(disposing);
        }

        private bool FillBuffer()
        {
            if (
                _bufferStart >= 0
                && _position >= _bufferStart
                && _position - _bufferStart < _bufferLength
            )
            {
                return true;
            }

            var buffer =
                _buffer ?? throw new ObjectDisposedException(nameof(RandomAccessBlockStream));
            var requested = (int)Math.Min(buffer.Length, _length - _position);
            var total = 0;
            while (total < requested)
            {
                var read = RandomAccess.Read(
                    _inputHandle,
                    buffer.AsSpan(total, requested - total),
                    checked(_start + _position + total)
                );
                if (read <= 0)
                {
                    break;
                }
                total += read;
            }

            _bufferStart = _position;
            _bufferLength = total;
            return total > 0;
        }
    }
#endif
}
