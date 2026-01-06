using System;
using System.Buffers.Binary;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using SharpCompress.Common;
using SharpCompress.Crypto;
using SharpCompress.IO;

namespace SharpCompress.Compressors.LZMA;

// TODO:
// - Write as well as read
// - Multi-volume support
// - Use of the data size / member size values at the end of the stream

/// <summary>
/// Stream supporting the LZIP format, as documented at http://www.nongnu.org/lzip/manual/lzip_manual.html
/// </summary>
public sealed class LZipStream : Stream, IStreamStack
{
#if DEBUG_STREAMS
    long IStreamStack.InstanceId { get; set; }
#endif
    int IStreamStack.DefaultBufferSize { get; set; }

    Stream IStreamStack.BaseStream() => _stream;

    int IStreamStack.BufferSize
    {
        get => 0;
        set { }
    }
    int IStreamStack.BufferPosition
    {
        get => 0;
        set { }
    }

    void IStreamStack.SetPosition(long position) { }

    private readonly Stream _stream;
    private readonly SharpCompressStream? _countingWritableSubStream;
    private bool _disposed;
    private bool _finished;

    private long _writeCount;
    private readonly Stream? _originalStream;

    public LZipStream(Stream stream, CompressionMode mode)
    {
        Mode = mode;
        _originalStream = stream;

        if (mode == CompressionMode.Decompress)
        {
            var dSize = ValidateAndReadSize(stream);
            if (dSize == 0)
            {
                throw new InvalidFormatException("Not an LZip stream");
            }
            var properties = GetProperties(dSize);
            _stream = new LzmaStream(properties, stream);
        }
        else
        {
            //default
            var dSize = 104 * 1024;
            WriteHeaderSize(stream);

            _countingWritableSubStream = new SharpCompressStream(stream, leaveOpen: true);
            _stream = new Crc32Stream(
                new LzmaStream(
                    new LzmaEncoderProperties(true, dSize),
                    false,
                    _countingWritableSubStream
                )
            );
#if DEBUG_STREAMS
            this.DebugConstruct(typeof(LZipStream));
#endif
        }
    }

    public void Finish()
    {
        if (!_finished)
        {
            if (Mode == CompressionMode.Compress)
            {
                var crc32Stream = (Crc32Stream)_stream;
                crc32Stream.WrappedStream.Dispose();
                crc32Stream.Dispose();
                var compressedCount = _countingWritableSubStream.NotNull().InternalPosition;

                Span<byte> intBuf = stackalloc byte[8];
                BinaryPrimitives.WriteUInt32LittleEndian(intBuf, crc32Stream.Crc);
                _countingWritableSubStream?.Write(intBuf.Slice(0, 4));

                BinaryPrimitives.WriteInt64LittleEndian(intBuf, _writeCount);
                _countingWritableSubStream?.Write(intBuf);

                //total with headers
                BinaryPrimitives.WriteUInt64LittleEndian(
                    intBuf,
                    (ulong)compressedCount + (ulong)(6 + 20)
                );
                _countingWritableSubStream?.Write(intBuf);
            }
            _finished = true;
        }
    }

    #region Stream methods

    protected override void Dispose(bool disposing)
    {
        if (_disposed)
        {
            return;
        }
        _disposed = true;
#if DEBUG_STREAMS
        this.DebugDispose(typeof(LZipStream));
#endif
        if (disposing)
        {
            Finish();
            _stream.Dispose();
            if (Mode == CompressionMode.Compress)
            {
                _originalStream?.Dispose();
            }
        }
    }

    public CompressionMode Mode { get; }

    public override bool CanRead => Mode == CompressionMode.Decompress;

    public override bool CanSeek => false;

    public override bool CanWrite => Mode == CompressionMode.Compress;

    public override void Flush() => _stream.Flush();

    // TODO: Both Length and Position are sometimes feasible, but would require
    // reading the output length when we initialize.
    public override long Length => throw new NotImplementedException();

    public override long Position
    {
        get => throw new NotImplementedException();
        set => throw new NotImplementedException();
    }

    public override int Read(byte[] buffer, int offset, int count) =>
        _stream.Read(buffer, offset, count);

    public override int ReadByte() => _stream.ReadByte();

    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

    public override void SetLength(long value) => throw new NotImplementedException();

#if !NETFRAMEWORK && !NETSTANDARD2_0

    public override ValueTask<int> ReadAsync(
        Memory<byte> buffer,
        CancellationToken cancellationToken = default
    ) => _stream.ReadAsync(buffer, cancellationToken);

    public override int Read(Span<byte> buffer) => _stream.Read(buffer);

    public override void Write(ReadOnlySpan<byte> buffer)
    {
        _stream.Write(buffer);

        _writeCount += buffer.Length;
    }
#endif

    public override void Write(byte[] buffer, int offset, int count)
    {
        _stream.Write(buffer, offset, count);
        _writeCount += count;
    }

    public override void WriteByte(byte value)
    {
        _stream.WriteByte(value);
        ++_writeCount;
    }

    public override Task<int> ReadAsync(
        byte[] buffer,
        int offset,
        int count,
        CancellationToken cancellationToken = default
    ) => _stream.ReadAsync(buffer, offset, count, cancellationToken);

    public override async Task WriteAsync(
        byte[] buffer,
        int offset,
        int count,
        CancellationToken cancellationToken
    )
    {
        cancellationToken.ThrowIfCancellationRequested();
        await _stream.WriteAsync(buffer, offset, count, cancellationToken);
        _writeCount += count;
    }

    #endregion

    /// <summary>
    /// Determines if the given stream is positioned at the start of a v1 LZip
    /// file, as indicated by the ASCII characters "LZIP" and a version byte
    /// of 1, followed by at least one byte.
    /// </summary>
    /// <param name="stream">The stream to read from. Must not be null.</param>
    /// <returns><c>true</c> if the given stream is an LZip file, <c>false</c> otherwise.</returns>
    public static bool IsLZipFile(Stream stream) => ValidateAndReadSize(stream) != 0;

    /// <summary>
    /// Reads the 6-byte header of the stream, and returns 0 if either the header
    /// couldn't be read or it isn't a validate LZIP header, or the dictionary
    /// size if it *is* a valid LZIP file.
    /// </summary>
    public static int ValidateAndReadSize(Stream stream)
    {
        // Read the header
        Span<byte> header = stackalloc byte[6];
        var n = stream.Read(header);

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

    private static readonly byte[] headerBytes =
    [
        (byte)'L',
        (byte)'Z',
        (byte)'I',
        (byte)'P',
        1,
        113,
    ];

    public static void WriteHeaderSize(Stream stream) =>
        // hard coding the dictionary size encoding
        stream.Write(headerBytes, 0, 6);

    /// <summary>
    /// Creates a byte array to communicate the parameters and dictionary size to LzmaStream.
    /// </summary>
    private static byte[] GetProperties(int dictionarySize) =>
        [
            // Parameters as per http://www.nongnu.org/lzip/manual/lzip_manual.html#Stream-format
            // but encoded as a single byte in the format LzmaStream expects.
            // literal_context_bits = 3
            // literal_pos_state_bits = 0
            // pos_state_bits = 2
            93,
            // Dictionary size as 4-byte little-endian value
            (byte)(dictionarySize & 0xff),
            (byte)((dictionarySize >> 8) & 0xff),
            (byte)((dictionarySize >> 16) & 0xff),
            (byte)((dictionarySize >> 24) & 0xff),
        ];
}
