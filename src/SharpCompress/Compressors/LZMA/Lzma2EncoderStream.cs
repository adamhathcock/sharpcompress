using System;
using System.Buffers;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using SharpCompress.IO;

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
    private readonly byte[] _properties;
    private readonly Encoder _encoder;
    private int _bufferPosition;
    private bool _isFirstChunk = true;
    private bool _isDisposed;
    private byte _lzmaPropertiesByte;

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
        _buffer = ArrayPool<byte>.Shared.Rent(MAX_UNCOMPRESSED_CHUNK_SIZE);
        _bufferPosition = 0;

        var encoderProps = new LzmaEncoderProperties(eos: false, _dictionarySize, _numFastBytes);
        _encoder = new Encoder();
        _encoder.SetCoderProperties(encoderProps.PropIDs, encoderProps.Properties);

        Span<byte> lzmaProperties = stackalloc byte[5];
        _encoder.WriteCoderProperties(lzmaProperties);
        _lzmaPropertiesByte = lzmaProperties[0];
        _properties = [EncodeDictionarySize(_dictionarySize)];
    }

    /// <summary>
    /// Gets the 1-byte LZMA2 properties (encoded dictionary size).
    /// </summary>
    public byte[] Properties => _properties;

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
            var toCopy = Math.Min(count, MAX_UNCOMPRESSED_CHUNK_SIZE - _bufferPosition);
            Buffer.BlockCopy(buffer, offset, _buffer, _bufferPosition, toCopy);
            _bufferPosition += toCopy;
            offset += toCopy;
            count -= toCopy;

            if (_bufferPosition == MAX_UNCOMPRESSED_CHUNK_SIZE)
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

            var toCopy = Math.Min(count, MAX_UNCOMPRESSED_CHUNK_SIZE - _bufferPosition);
            Buffer.BlockCopy(buffer, offset, _buffer, _bufferPosition, toCopy);
            _bufferPosition += toCopy;
            offset += toCopy;
            count -= toCopy;

            if (_bufferPosition == MAX_UNCOMPRESSED_CHUNK_SIZE)
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

            var toCopy = Math.Min(count, MAX_UNCOMPRESSED_CHUNK_SIZE - _bufferPosition);
            buffer.Slice(offset, toCopy).Span.CopyTo(_buffer.AsSpan(_bufferPosition, toCopy));
            _bufferPosition += toCopy;
            offset += toCopy;
            count -= toCopy;

            if (_bufferPosition == MAX_UNCOMPRESSED_CHUNK_SIZE)
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
            _encoder.Dispose();
            ArrayPool<byte>.Shared.Return(_buffer);
        }
        base.Dispose(disposing);
    }

    private static readonly byte[] _endMarker = [0x00];

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
#if !LEGACY_DOTNET || NETSTANDARD2_1

            await _output.WriteAsync(new Memory<byte>(_endMarker)).ConfigureAwait(false);
#else
            await _output.WriteAsync(_endMarker, 0, 1).ConfigureAwait(false);
#endif

            _encoder.Dispose();
            ArrayPool<byte>.Shared.Return(_buffer);
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

        var uncompressedSize = _bufferPosition;
        var uncompressedData = _buffer.AsSpan(0, uncompressedSize);
        _bufferPosition = 0;

        // Try compressing the data
        (byte[] Buffer, int Length) compressed;
        try
        {
            compressed = CompressBlock(_buffer, uncompressedSize);
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
            WriteCompressedChunk(uncompressedData.Length, compressed.Buffer, compressed.Length);
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

        var uncompressedSize = _bufferPosition;
        var uncompressedData = _buffer.AsMemory(0, uncompressedSize);
        _bufferPosition = 0;

        (byte[] Buffer, int Length) compressed;
        try
        {
            compressed = CompressBlock(_buffer, uncompressedSize);
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
            await WriteCompressedChunkAsync(
                    uncompressedData.Length,
                    compressed.Buffer,
                    compressed.Length,
                    cancellationToken
                )
                .ConfigureAwait(false);
        }
        else
        {
            await WriteUncompressedChunksAsync(uncompressedData, cancellationToken)
                .ConfigureAwait(false);
        }
    }

    private (byte[] Buffer, int Length) CompressBlock(byte[] data, int length)
    {
        using var inputMs = new MemoryStream(data, 0, length, writable: false);
        using var outputMs = new PooledMemoryStream();

        _encoder.Code(inputMs, outputMs, length, -1, null);

        var fullCompressed = outputMs.ToArray();

        // The LZMA range encoder flush writes trailing bytes the decoder doesn't consume.
        // Trial-decode to find the exact byte count the decoder needs, so the LZMA2
        // chunk header reports a compressed size that matches what the decoder reads.
        var consumed = FindConsumedBytes(fullCompressed, fullCompressed.Length, length);
        return (fullCompressed, consumed);
    }

    private int FindConsumedBytes(byte[] compressedData, int compressedSize, int uncompressedSize)
    {
        // Build 5-byte LZMA property header: [pb/lp/lc byte] [dictSize as LE int32]
        Span<byte> props = stackalloc byte[5];
        props[0] = _lzmaPropertiesByte;
        props[1] = (byte)_dictionarySize;
        props[2] = (byte)(_dictionarySize >> 8);
        props[3] = (byte)(_dictionarySize >> 16);
        props[4] = (byte)(_dictionarySize >> 24);

        var decoder = new Decoder();
        decoder.SetDecoderProperties(props);

        using var input = new MemoryStream(compressedData, 0, compressedSize, writable: false);
        decoder.Code(input, Stream.Null, compressedSize, uncompressedSize, null);

        return (int)input.Position;
    }

    /// <summary>
    /// Writes a compressed LZMA2 chunk.
    /// Header: [control] [uncompSize_hi] [uncompSize_lo] [compSize_hi] [compSize_lo] [props?]
    /// </summary>
    private void WriteCompressedChunk(
        int uncompressedSize,
        byte[] compressedData,
        int compressedSize
    )
    {
        var uncompSizeMinus1 = uncompressedSize - 1;
        var compSizeMinus1 = compressedSize - 1;

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

        _output.Write(compressedData, 0, compressedSize);
    }

    private async ValueTask WriteCompressedChunkAsync(
        int uncompressedSize,
        byte[] compressedData,
        int compressedSize,
        CancellationToken cancellationToken = default
    )
    {
        var uncompSizeMinus1 = uncompressedSize - 1;
        var compSizeMinus1 = compressedSize - 1;
        var control = (byte)(0xE0 | ((uncompSizeMinus1 >> 16) & 0x1F));
        _isFirstChunk = false;

        var header = ArrayPool<byte>.Shared.Rent(6);
        try
        {
            header[0] = control;
            header[1] = (byte)((uncompSizeMinus1 >> 8) & 0xFF);
            header[2] = (byte)(uncompSizeMinus1 & 0xFF);
            header[3] = (byte)((compSizeMinus1 >> 8) & 0xFF);
            header[4] = (byte)(compSizeMinus1 & 0xFF);
            header[5] = _lzmaPropertiesByte;
            await _output.WriteAsync(header, 0, 6, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(header);
        }
        await _output
            .WriteAsync(compressedData, 0, compressedSize, cancellationToken)
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

            var header = ArrayPool<byte>.Shared.Rent(3);
            try
            {
                header[0] = control;
                header[1] = (byte)((sizeMinus1 >> 8) & 0xFF);
                header[2] = (byte)(sizeMinus1 & 0xFF);
                await _output.WriteAsync(header, 0, 3, cancellationToken).ConfigureAwait(false);
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(header);
            }

#if !LEGACY_DOTNET || NETSTANDARD2_1
            await _output
                .WriteAsync(data.Slice(offset, chunkSize), cancellationToken)
                .ConfigureAwait(false);
#else
            var chunk = ArrayPool<byte>.Shared.Rent(chunkSize);
            try
            {
                data.Slice(offset, chunkSize).CopyTo(chunk);
                await _output
                    .WriteAsync(chunk, 0, chunkSize, cancellationToken)
                    .ConfigureAwait(false);
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(chunk);
            }
#endif
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
