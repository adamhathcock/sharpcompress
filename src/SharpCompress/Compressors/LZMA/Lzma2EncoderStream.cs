using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace SharpCompress.Compressors.LZMA;

/// <summary>
/// Write-only stream that compresses data using the LZMA2 framing format.
/// Buffers input, compresses in chunks, and writes LZMA2-framed output to the underlying stream.
/// Each chunk is independently compressed with a fresh LZMA encoder.
/// </summary>
internal sealed class Lzma2EncoderStream : Stream
{
    // Max uncompressed chunk size per LZMA2 spec: (0x1F << 16) + 0xFFFF + 1 = 2MB
    private const int MAX_UNCOMPRESSED_CHUNK_SIZE = (0x1F << 16) + 0xFFFF + 1;

    // Max compressed payload per LZMA2 chunk header: 0xFFFF + 1 = 64KB
    private const int MAX_COMPRESSED_CHUNK_SIZE = 0xFFFF + 1;

    // Max uncompressed sub-chunk for raw (uncompressed) chunks: 0xFFFF + 1 = 64KB
    private const int MAX_UNCOMPRESSED_SUBCHUNK_SIZE = 0xFFFF + 1;

    private readonly Stream _output;
    private readonly int _dictionarySize;
    private readonly int _numFastBytes;
    private readonly byte[] _buffer;
    private int _bufferPosition;
    private bool _isFirstChunk = true;
    private bool _isDisposed;
    private byte _lzmaPropertiesByte;
    private bool _lzmaPropertiesKnown;

    /// <summary>
    /// Creates a new LZMA2 encoder stream.
    /// </summary>
    /// <param name="output">The stream to write LZMA2-framed compressed data to.</param>
    /// <param name="dictionarySize">Dictionary size for LZMA compression.</param>
    /// <param name="numFastBytes">Number of fast bytes for LZMA compression.</param>
    public Lzma2EncoderStream(Stream output, int dictionarySize, int numFastBytes)
    {
        _output = output;
        _dictionarySize = dictionarySize;
        _numFastBytes = numFastBytes;
        _buffer = new byte[MAX_UNCOMPRESSED_CHUNK_SIZE];
        _bufferPosition = 0;
    }

    /// <summary>
    /// Gets the 1-byte LZMA2 properties (encoded dictionary size).
    /// </summary>
    public byte[] Properties => [EncodeDictionarySize(_dictionarySize)];

    public override bool CanRead => false;
    public override bool CanSeek => false;
    public override bool CanWrite => true;
    public override long Length => throw new NotSupportedException();

    public override long Position
    {
        get => throw new NotSupportedException();
        set => throw new NotSupportedException();
    }

    public override void Write(byte[] buffer, int offset, int count)
    {
        while (count > 0)
        {
            var toCopy = Math.Min(count, _buffer.Length - _bufferPosition);
            Buffer.BlockCopy(buffer, offset, _buffer, _bufferPosition, toCopy);
            _bufferPosition += toCopy;
            offset += toCopy;
            count -= toCopy;

            if (_bufferPosition == _buffer.Length)
            {
                FlushChunk();
            }
        }
    }

    public override async Task WriteAsync(
        byte[] buffer,
        int offset,
        int count,
        CancellationToken cancellationToken
    )
    {
        while (count > 0)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var toCopy = Math.Min(count, _buffer.Length - _bufferPosition);
            Buffer.BlockCopy(buffer, offset, _buffer, _bufferPosition, toCopy);
            _bufferPosition += toCopy;
            offset += toCopy;
            count -= toCopy;

            if (_bufferPosition == _buffer.Length)
            {
                await FlushChunkAsync(cancellationToken).ConfigureAwait(false);
            }
        }
    }

#if !LEGACY_DOTNET
    public override async ValueTask WriteAsync(
        ReadOnlyMemory<byte> buffer,
        CancellationToken cancellationToken = default
    )
    {
        var offset = 0;
        var count = buffer.Length;
        while (count > 0)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var toCopy = Math.Min(count, _buffer.Length - _bufferPosition);
            buffer.Slice(offset, toCopy).Span.CopyTo(_buffer.AsSpan(_bufferPosition, toCopy));
            _bufferPosition += toCopy;
            offset += toCopy;
            count -= toCopy;

            if (_bufferPosition == _buffer.Length)
            {
                await FlushChunkAsync(cancellationToken).ConfigureAwait(false);
            }
        }
    }
#endif

    public override void Flush() { }

    public override int Read(byte[] buffer, int offset, int count) =>
        throw new NotSupportedException();

    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

    public override void SetLength(long value) => throw new NotSupportedException();

    protected override void Dispose(bool disposing)
    {
        if (disposing && !_isDisposed)
        {
            _isDisposed = true;

            // Flush remaining buffered data
            if (_bufferPosition > 0)
            {
                FlushChunk();
            }

            // Write LZMA2 end marker
            _output.WriteByte(0x00);
        }
        base.Dispose(disposing);
    }

#if !LEGACY_DOTNET || NETSTANDARD2_1
    public override async ValueTask DisposeAsync()
#else
    public async ValueTask DisposeAsync()
#endif
    {
        if (!_isDisposed)
        {
            _isDisposed = true;

            if (_bufferPosition > 0)
            {
                await FlushChunkAsync().ConfigureAwait(false);
            }

            await _output.WriteAsync(new byte[] { 0x00 }, 0, 1).ConfigureAwait(false);
        }

#if !LEGACY_DOTNET || NETSTANDARD2_1
        await base.DisposeAsync().ConfigureAwait(false);
#endif
    }

    private void FlushChunk()
    {
        if (_bufferPosition == 0)
        {
            return;
        }

        var uncompressedData = _buffer.AsSpan(0, _bufferPosition);
        _bufferPosition = 0;

        // Try compressing the data
        byte[] compressed;
        try
        {
            compressed = CompressBlock(uncompressedData);
        }
        catch (Exception ex) when (ex is not OutOfMemoryException and not StackOverflowException)
        {
            // If compression fails, write as uncompressed
            WriteUncompressedChunks(uncompressedData);
            return;
        }

        // Check if compressed output fits in a single chunk and is actually smaller
        if (
            compressed.Length <= MAX_COMPRESSED_CHUNK_SIZE
            && compressed.Length < uncompressedData.Length
        )
        {
            WriteCompressedChunk(uncompressedData.Length, compressed);
        }
        else
        {
            WriteUncompressedChunks(uncompressedData);
        }
    }

    private async ValueTask FlushChunkAsync(CancellationToken cancellationToken = default)
    {
        if (_bufferPosition == 0)
        {
            return;
        }

        var uncompressedData = _buffer.AsMemory(0, _bufferPosition);
        _bufferPosition = 0;

        byte[] compressed;
        try
        {
            compressed = CompressBlock(uncompressedData.Span);
        }
        catch (Exception ex) when (ex is not OutOfMemoryException and not StackOverflowException)
        {
            await WriteUncompressedChunksAsync(uncompressedData, cancellationToken)
                .ConfigureAwait(false);
            return;
        }

        if (
            compressed.Length <= MAX_COMPRESSED_CHUNK_SIZE
            && compressed.Length < uncompressedData.Length
        )
        {
            await WriteCompressedChunkAsync(uncompressedData.Length, compressed, cancellationToken)
                .ConfigureAwait(false);
        }
        else
        {
            await WriteUncompressedChunksAsync(uncompressedData, cancellationToken)
                .ConfigureAwait(false);
        }
    }

    private byte[] CompressBlock(ReadOnlySpan<byte> data)
    {
        var encoderProps = new LzmaEncoderProperties(eos: false, _dictionarySize, _numFastBytes);

        var encoder = new Encoder();
        encoder.SetCoderProperties(encoderProps.PropIDs, encoderProps.Properties);

        // Capture the LZMA properties byte (pb/lp/lc encoding) for the chunk header
        if (!_lzmaPropertiesKnown)
        {
            var propBytes = new byte[5];
            encoder.WriteCoderProperties(propBytes);
            _lzmaPropertiesByte = propBytes[0];
            _lzmaPropertiesKnown = true;
        }

        using var inputMs = new MemoryStream(data.ToArray(), writable: false);
        using var outputMs = new MemoryStream();

        encoder.Code(inputMs, outputMs, data.Length, -1, null);

        var fullCompressed = outputMs.ToArray();

        // The LZMA range encoder flush writes trailing bytes the decoder doesn't consume.
        // Trial-decode to find the exact byte count the decoder needs, so the LZMA2
        // chunk header reports a compressed size that matches what the decoder reads.
        var consumed = FindConsumedBytes(fullCompressed, data.Length);
        if (consumed < fullCompressed.Length)
        {
            return fullCompressed.AsSpan(0, consumed).ToArray();
        }

        return fullCompressed;
    }

    private int FindConsumedBytes(byte[] compressedData, int uncompressedSize)
    {
        // Build 5-byte LZMA property header: [pb/lp/lc byte] [dictSize as LE int32]
        var props = new byte[5];
        props[0] = _lzmaPropertiesByte;
        props[1] = (byte)_dictionarySize;
        props[2] = (byte)(_dictionarySize >> 8);
        props[3] = (byte)(_dictionarySize >> 16);
        props[4] = (byte)(_dictionarySize >> 24);

        var decoder = new Decoder();
        decoder.SetDecoderProperties(props);

        using var input = new MemoryStream(compressedData);
        using var output = new MemoryStream();
        decoder.Code(input, output, compressedData.Length, uncompressedSize, null);

        return (int)input.Position;
    }

    /// <summary>
    /// Writes a compressed LZMA2 chunk.
    /// Header: [control] [uncompSize_hi] [uncompSize_lo] [compSize_hi] [compSize_lo] [props?]
    /// </summary>
    private void WriteCompressedChunk(int uncompressedSize, byte[] compressedData)
    {
        var uncompSizeMinus1 = uncompressedSize - 1;
        var compSizeMinus1 = compressedData.Length - 1;

        // Each chunk is compressed independently with a fresh LZMA encoder,
        // so we must use 0xE0 (full reset: dictionary + state + properties) every time.
        // The decoder uses outWindow.Total for literal context and posState;
        // 0xE0 triggers outWindow.Reset() which zeros Total, matching the encoder's
        // assumption that position starts at 0 for each chunk.
        var control = (byte)(0xE0 | ((uncompSizeMinus1 >> 16) & 0x1F));
        _isFirstChunk = false;

        _output.WriteByte(control);
        _output.WriteByte((byte)((uncompSizeMinus1 >> 8) & 0xFF));
        _output.WriteByte((byte)(uncompSizeMinus1 & 0xFF));
        _output.WriteByte((byte)((compSizeMinus1 >> 8) & 0xFF));
        _output.WriteByte((byte)(compSizeMinus1 & 0xFF));

        // 0xE0 (>= 0xC0) requires properties byte
        _output.WriteByte(_lzmaPropertiesByte);

        _output.Write(compressedData, 0, compressedData.Length);
    }

    private async ValueTask WriteCompressedChunkAsync(
        int uncompressedSize,
        byte[] compressedData,
        CancellationToken cancellationToken = default
    )
    {
        var uncompSizeMinus1 = uncompressedSize - 1;
        var compSizeMinus1 = compressedData.Length - 1;
        var control = (byte)(0xE0 | ((uncompSizeMinus1 >> 16) & 0x1F));
        _isFirstChunk = false;

        var header = new[]
        {
            control,
            (byte)((uncompSizeMinus1 >> 8) & 0xFF),
            (byte)(uncompSizeMinus1 & 0xFF),
            (byte)((compSizeMinus1 >> 8) & 0xFF),
            (byte)(compSizeMinus1 & 0xFF),
            _lzmaPropertiesByte,
        };
        await _output.WriteAsync(header, 0, header.Length, cancellationToken).ConfigureAwait(false);
        await _output
            .WriteAsync(compressedData, 0, compressedData.Length, cancellationToken)
            .ConfigureAwait(false);
    }

    /// <summary>
    /// Writes data as uncompressed LZMA2 sub-chunks (max 64KB each).
    /// Header: [control] [size_hi] [size_lo]
    /// </summary>
    private void WriteUncompressedChunks(ReadOnlySpan<byte> data)
    {
        var offset = 0;
        while (offset < data.Length)
        {
            var chunkSize = Math.Min(data.Length - offset, MAX_UNCOMPRESSED_SUBCHUNK_SIZE);
            var sizeMinus1 = chunkSize - 1;

            byte control;
            if (_isFirstChunk)
            {
                // 0x01: uncompressed with dictionary reset
                control = 0x01;
                _isFirstChunk = false;
            }
            else
            {
                // 0x02: uncompressed without dictionary reset
                control = 0x02;
            }

            _output.WriteByte(control);
            _output.WriteByte((byte)((sizeMinus1 >> 8) & 0xFF));
            _output.WriteByte((byte)(sizeMinus1 & 0xFF));

            _output.Write(data.Slice(offset, chunkSize));
            offset += chunkSize;
        }
    }

    private async ValueTask WriteUncompressedChunksAsync(
        ReadOnlyMemory<byte> data,
        CancellationToken cancellationToken = default
    )
    {
        var offset = 0;
        while (offset < data.Length)
        {
            var chunkSize = Math.Min(data.Length - offset, MAX_UNCOMPRESSED_SUBCHUNK_SIZE);
            var sizeMinus1 = chunkSize - 1;

            byte control;
            if (_isFirstChunk)
            {
                control = 0x01;
                _isFirstChunk = false;
            }
            else
            {
                control = 0x02;
            }

            var header = new[]
            {
                control,
                (byte)((sizeMinus1 >> 8) & 0xFF),
                (byte)(sizeMinus1 & 0xFF),
            };
            await _output
                .WriteAsync(header, 0, header.Length, cancellationToken)
                .ConfigureAwait(false);

            var chunk = data.Slice(offset, chunkSize).ToArray();
            await _output
                .WriteAsync(chunk, 0, chunk.Length, cancellationToken)
                .ConfigureAwait(false);
            offset += chunkSize;
        }
    }

    /// <summary>
    /// Encodes a dictionary size into the 1-byte LZMA2 properties format.
    /// Reverse of the decoder formula: dictSize = (2 | (p and 1)) shl ((p shr 1) + 11)
    /// Finds the smallest p where the formula result >= target dictSize.
    /// </summary>
    internal static byte EncodeDictionarySize(int dictSize)
    {
        // Special case: very small dictionary sizes
        if (dictSize <= (2 << 11))
        {
            return 0;
        }

        for (byte p = 0; p < 40; p++)
        {
            var shift = (p >> 1) + 11;
            if (shift >= 31)
            {
                return p;
            }

            var size = (2 | (p & 1)) << shift;
            if (size >= dictSize)
            {
                return p;
            }
        }

        return 40;
    }
}
