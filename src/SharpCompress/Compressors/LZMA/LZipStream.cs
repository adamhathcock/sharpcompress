using System;
using System.Buffers.Binary;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using SharpCompress.Common;
using SharpCompress.Crypto;
using SharpCompress.IO;
using SharpCompress.Providers;

namespace SharpCompress.Compressors.LZMA;

// TODO:
// - Write as well as read
// - Multi-volume support
// - Use of the data size / member size values at the end of the stream

/// <summary>
/// Stream supporting the LZIP format, as documented at http://www.nongnu.org/lzip/manual/lzip_manual.html
/// </summary>
public sealed partial class LZipStream : Stream, IFinishable
{
    private readonly Stream _stream;
    private readonly CountingStream? _countingWritableSubStream;
    private readonly CountingStream? _countingReadableSubStream;
    private readonly uint[]? _crc32Table;
    private readonly ulong? _expectedDataSize;
    private readonly ulong? _expectedMemberSize;
    private readonly bool _skipTrailerValidation;
    private bool _disposed;
    private bool _finished;
    private bool _trailerValidated;
    private uint _seed = Crc32Stream.DEFAULT_SEED;
    private ulong _readCount;
    private readonly long _memberStartPosition;
    private readonly long _compressedDataStartPosition;

    private long _writeCount;
    private readonly Stream? _originalStream;
    private readonly bool _leaveOpen;

    private LZipStream(Stream stream, CompressionMode mode, bool leaveOpen = false)
    {
        Mode = mode;
        _originalStream = stream;
        _leaveOpen = leaveOpen;

        if (mode == CompressionMode.Decompress)
        {
            _skipTrailerValidation = stream is SharpCompressStream;
            _memberStartPosition = stream.CanSeek ? stream.Position : 0;
            var dSize = ValidateAndReadSize(stream);
            if (dSize == 0)
            {
                throw new InvalidFormatException("Not an LZip stream");
            }
            var properties = GetProperties(dSize);
            var trailerStream = GetSeekableTrailerStream(stream);
            if (trailerStream is not null)
            {
                var position = trailerStream.Position;
                trailerStream.Position = trailerStream.Length - 16;
                Span<byte> sizeTrailer = stackalloc byte[16];
                trailerStream.ReadFully(sizeTrailer);
                _expectedDataSize = BinaryPrimitives.ReadUInt64LittleEndian(sizeTrailer);
                _expectedMemberSize = BinaryPrimitives.ReadUInt64LittleEndian(sizeTrailer[8..]);
                if (_expectedDataSize > long.MaxValue)
                {
                    throw new InvalidFormatException("LZip data size is too large.");
                }
                trailerStream.Position = position;
            }
            _compressedDataStartPosition = stream.CanSeek ? stream.Position : 0;
            _countingReadableSubStream = new CountingStream(
                SharpCompressStream.CreateNonDisposing(stream)
            );
            _crc32Table = Crc32Stream.InitializeTable(Crc32Stream.DEFAULT_POLYNOMIAL);
            _stream = LzmaStream.Create(
                properties,
                _countingReadableSubStream,
                inputSize: -1,
                outputSize: _expectedDataSize.HasValue
                    ? checked((long)_expectedDataSize.Value)
                    : -1,
                leaveOpen: leaveOpen
            );
        }
        else
        {
            //default
            var dSize = 104 * 1024;
            _countingWritableSubStream = new CountingStream(
                SharpCompressStream.CreateNonDisposing(stream)
            );
            _stream = new Crc32Stream(
                LzmaStream.Create(
                    new LzmaEncoderProperties(true, dSize),
                    false,
                    null,
                    _countingWritableSubStream
                )
            );
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
                var compressedCount = _countingWritableSubStream.NotNull().BytesWritten;

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
            base.Dispose(disposing);
            return;
        }
        _disposed = true;
        if (disposing)
        {
            Finish();
            _stream.Dispose();
            if (!_leaveOpen)
            {
                _originalStream?.Dispose();
            }
        }
        base.Dispose(disposing);
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

    public override int Read(byte[] buffer, int offset, int count)
    {
        var read = _stream.Read(buffer, offset, count);
        UpdateAndValidateAtEof(buffer.AsSpan(offset, read), read);
        return read;
    }

    public override int ReadByte()
    {
        var value = _stream.ReadByte();
        if (value == -1)
        {
            ValidateTrailer();
        }
        else
        {
            Span<byte> buffer = stackalloc byte[1];
            buffer[0] = (byte)value;
            UpdateChecksum(buffer);
        }

        return value;
    }

    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

    public override void SetLength(long value) => throw new NotImplementedException();

#if !LEGACY_DOTNET

    public override int Read(Span<byte> buffer)
    {
        var read = _stream.Read(buffer);
        UpdateAndValidateAtEof(buffer[..read], read);
        return read;
    }

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

    // Async methods moved to LZipStream.Async.cs

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
        if (!stream.ReadFully(header))
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
        return DecodeDictionarySize(header[5]);
    }

    private static int DecodeDictionarySize(byte encoded)
    {
        var basePower = encoded & 0x1F;
        if (basePower < 12 || basePower > 29)
        {
            return 0;
        }
        var size = (1 << basePower) - ((encoded >> 5) * (1 << (basePower - 4)));
        return size >= 4096 ? size : 0;
    }

    // Async methods moved to LZipStream.Async.cs

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

    private static Stream? GetSeekableTrailerStream(Stream stream)
    {
        while (stream is SharpCompressStream { IsPassthrough: true } sharpCompressStream)
        {
            stream = sharpCompressStream.BaseStream();
        }

        if (stream is SeekableSharpCompressStream seekableSharpCompressStream)
        {
            stream = seekableSharpCompressStream.BaseStream();
        }

        return stream is SharpCompressStream ? null
            : stream.CanSeek ? stream
            : null;
    }

    private static Stream? GetPhysicalSeekableStream(Stream stream)
    {
        while (stream is SharpCompressStream sharpCompressStream)
        {
            var baseStream = sharpCompressStream.BaseStream();
            if (ReferenceEquals(baseStream, stream) || !baseStream.CanSeek)
            {
                break;
            }

            stream = baseStream;
        }

        return stream.CanSeek ? stream : null;
    }

    private static bool IsProbeWrapper(Stream stream) =>
        stream is SharpCompressStream { IsPassthrough: true } sharpCompressStream
        && sharpCompressStream.BaseStream() is SharpCompressStream { IsPassthrough: false };

    private void UpdateAndValidateAtEof(ReadOnlySpan<byte> buffer, int read)
    {
        if (Mode != CompressionMode.Decompress)
        {
            return;
        }

        if (read > 0)
        {
            UpdateChecksum(buffer);
            return;
        }

        ValidateTrailer();
    }

    private void UpdateChecksum(ReadOnlySpan<byte> buffer)
    {
        _seed = Crc32Stream.CalculateCrc(_crc32Table.NotNull(), _seed, buffer);
        _readCount += (ulong)buffer.Length;
    }

    private void ValidateTrailer()
    {
        if (_trailerValidated || _skipTrailerValidation || Mode != CompressionMode.Decompress)
        {
            return;
        }

        _trailerValidated = true;

        var countingStream = _countingReadableSubStream.NotNull();
        ulong? compressedDataSize = null;
        Span<byte> trailer = stackalloc byte[20];
        if (_expectedMemberSize.HasValue && countingStream.CanSeek)
        {
            compressedDataSize = _expectedMemberSize.Value - 26;
            countingStream.Position = _compressedDataStartPosition + (long)compressedDataSize.Value;
            countingStream.ReadFully(trailer);
        }
        else if (GetPhysicalSeekableStream(countingStream.WrappedStream) is { } trailerStream)
        {
            var position = trailerStream.Position;
            trailerStream.Position = trailerStream.Length - 20;
            trailerStream.ReadFully(trailer);
            trailerStream.Position = position;
        }
        else
        {
            compressedDataSize = _stream is LzmaStream lzmaStream
                ? (ulong)lzmaStream.CompressedBytesRead
                : (ulong)countingStream.BytesRead;
            if (countingStream.CanSeek)
            {
                countingStream.Position =
                    _compressedDataStartPosition + (long)compressedDataSize.Value;
            }
            countingStream.ReadFully(trailer);
        }

        var expectedCrc = BinaryPrimitives.ReadUInt32LittleEndian(trailer);
        var expectedDataSize = BinaryPrimitives.ReadUInt64LittleEndian(trailer[4..]);
        var expectedMemberSize = BinaryPrimitives.ReadUInt64LittleEndian(trailer[12..]);

        var actualCrc = ~_seed;
        if (actualCrc != expectedCrc)
        {
            throw new InvalidFormatException(
                $"LZip CRC mismatch. Expected 0x{expectedCrc:X8}, actual 0x{actualCrc:X8}."
            );
        }

        if (_readCount != expectedDataSize)
        {
            throw new InvalidFormatException(
                $"LZip data size mismatch. Expected {expectedDataSize}, actual {_readCount}."
            );
        }

        var actualMemberSize = compressedDataSize ?? expectedMemberSize - 26;
        actualMemberSize += 26;
        if (actualMemberSize != expectedMemberSize)
        {
            throw new InvalidFormatException(
                $"LZip member size mismatch. Expected {expectedMemberSize}, actual {actualMemberSize}."
            );
        }
    }
}
