using System;
using System.Buffers.Binary;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using SharpCompress.Crypto;
using SharpCompress.IO;

namespace SharpCompress.Compressors.LZMA;

public sealed partial class LZipStream
{
    public static ValueTask<LZipStream> CreateAsync(
        Stream stream,
        CompressionMode mode,
        bool leaveOpen = false,
        CancellationToken cancellationToken = default
    )
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (mode != CompressionMode.Compress)
        {
            return new ValueTask<LZipStream>(new LZipStream(stream, mode, leaveOpen));
        }

        // The LZMA encoder used for LZip currently finalizes synchronously, so the async
        // creation path compresses to memory and writes the completed member asynchronously.
        var compressedBuffer = new MemoryStream();
        return new ValueTask<LZipStream>(
            new LZipStream(compressedBuffer, mode, leaveOpen, stream, compressedBuffer)
        );
    }

    public async ValueTask FinishAsync()
    {
        if (_finished)
        {
            return;
        }

        if (Mode == CompressionMode.Compress)
        {
            var crc32Stream = (Crc32Stream)_stream;
            FinishWrappedStream(crc32Stream);
            var compressedCount = _countingWritableSubStream.NotNull().BytesWritten;

            var intBuf = new byte[8];
            BinaryPrimitives.WriteUInt32LittleEndian(intBuf, crc32Stream.Crc);
            await _countingWritableSubStream
                .NotNull()
                .WriteAsync(intBuf, 0, 4, CancellationToken.None)
                .ConfigureAwait(false);

            BinaryPrimitives.WriteInt64LittleEndian(intBuf, _writeCount);
            await _countingWritableSubStream
                .NotNull()
                .WriteAsync(intBuf, 0, intBuf.Length, CancellationToken.None)
                .ConfigureAwait(false);

            // Total member size includes the 6-byte header and 20-byte trailer.
            BinaryPrimitives.WriteUInt64LittleEndian(
                intBuf,
                (ulong)compressedCount + (ulong)(6 + 20)
            );
            await _countingWritableSubStream
                .NotNull()
                .WriteAsync(intBuf, 0, intBuf.Length, CancellationToken.None)
                .ConfigureAwait(false);

            if (_asyncCompressedBuffer is not null && _asyncFinalDestination is not null)
            {
                _asyncCompressedBuffer.Position = 0;
                await _asyncCompressedBuffer
                    .CopyToAsync(_asyncFinalDestination, 81920, CancellationToken.None)
                    .ConfigureAwait(false);
            }
        }

        _finished = true;
    }

    private static void FinishWrappedStream(Crc32Stream crc32Stream)
    {
        crc32Stream.WrappedStream.Dispose();
        crc32Stream.Dispose();
    }

    /// <summary>
    /// Asynchronously determines if the given stream is positioned at the start of a v1 LZip
    /// file, as indicated by the ASCII characters "LZIP" and a version byte
    /// of 1, followed by at least one byte.
    /// </summary>
    /// <param name="stream">The stream to read from. Must not be null.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns><c>true</c> if the given stream is an LZip file, <c>false</c> otherwise.</returns>
    public static async ValueTask<bool> IsLZipFileAsync(
        Stream stream,
        CancellationToken cancellationToken = default
    ) => await ValidateAndReadSizeAsync(stream, cancellationToken).ConfigureAwait(false) != 0;

    /// <summary>
    /// Asynchronously reads the 6-byte header of the stream, and returns 0 if either the header
    /// couldn't be read or it isn't a validate LZIP header, or the dictionary
    /// size if it *is* a valid LZIP file.
    /// </summary>
    public static async ValueTask<int> ValidateAndReadSizeAsync(
        Stream stream,
        CancellationToken cancellationToken
    )
    {
        // Read the header
        byte[] header = new byte[6];
        var n = await stream
            .ReadAsync(header, 0, header.Length, cancellationToken)
            .ConfigureAwait(false);

        // TODO: Handle reading only part of the header?

        if (n != 6)
        {
            return 0;
        }

        if (
            header[0] != 'L'
            || header[1] != 'Z'
            || header[2] != 'I'
            || header[3] != 'P'
            || header[4] != 1 /* version 1 */
        )
        {
            return 0;
        }
        var basePower = header[5] & 0x1F;
        var subtractionNumerator = (header[5] & 0xE0) >> 5;
        return (1 << basePower) - (subtractionNumerator * (1 << (basePower - 4)));
    }

#if !LEGACY_DOTNET
    /// <summary>
    /// Asynchronously reads bytes from the current stream into a buffer.
    /// </summary>
    public override ValueTask<int> ReadAsync(
        Memory<byte> buffer,
        CancellationToken cancellationToken = default
    ) => _stream.ReadAsync(buffer, cancellationToken);
#endif

    /// <summary>
    /// Asynchronously reads bytes from the current stream into a buffer.
    /// </summary>
    public override Task<int> ReadAsync(
        byte[] buffer,
        int offset,
        int count,
        CancellationToken cancellationToken = default
    ) => _stream.ReadAsync(buffer, offset, count, cancellationToken);

    /// <summary>
    /// Asynchronously writes bytes from a buffer to the current stream.
    /// </summary>
    public override async Task WriteAsync(
        byte[] buffer,
        int offset,
        int count,
        CancellationToken cancellationToken
    )
    {
        cancellationToken.ThrowIfCancellationRequested();
        await _stream.WriteAsync(buffer, offset, count, cancellationToken).ConfigureAwait(false);
        _writeCount += count;
    }
}
