using System;
using System.Buffers;
using System.Buffers.Binary;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using SharpCompress.Crypto;
using SharpCompress.IO;

namespace SharpCompress.Compressors.LZMA
{
    // TODO:
    // - Write as well as read
    // - Multi-volume support
    // - Use of the data size / member size values at the end of the stream

    /// <summary>
    /// Stream supporting the LZIP format, as documented at http://www.nongnu.org/lzip/manual/lzip_manual.html
    /// </summary>
    public sealed class LZipStream : AsyncStream
    {
#nullable disable
        private Stream _stream;
#nullable enable
        private CountingWritableSubStream? _countingWritableSubStream;
        private bool _disposed;
        private bool _finished;

        private long _writeCount;

        private LZipStream()
        {
            
        }

        public static async ValueTask<LZipStream> CreateAsync(Stream stream, CompressionMode mode)
        {
            var lzip = new LZipStream();
            lzip.Mode = mode;

            if (mode == CompressionMode.Decompress)
            {
                int dSize = await ValidateAndReadSize(stream);
                if (dSize == 0)
                {
                    throw new IOException("Not an LZip stream");
                }
                byte[] properties = GetProperties(dSize);
                lzip._stream = await LzmaStream.CreateAsync(properties, stream);
            }
            else
            {
                //default
                int dSize = 104 * 1024;
                await WriteHeaderSizeAsync(stream);

                lzip._countingWritableSubStream = new CountingWritableSubStream(stream);
                lzip._stream = new Crc32Stream(new LzmaStream(new LzmaEncoderProperties(true, dSize), false, lzip._countingWritableSubStream));
            }
            return lzip;
        }

        public async ValueTask FinishAsync()
        {
            if (!_finished)
            {
                if (Mode == CompressionMode.Compress)
                {
                    var crc32Stream = (Crc32Stream)_stream;
                    await crc32Stream.WrappedStream.DisposeAsync();
                    await crc32Stream.DisposeAsync();
                    var compressedCount = _countingWritableSubStream!.Count;

                    byte[] intBuf = new byte[8];
                    BinaryPrimitives.WriteUInt32LittleEndian(intBuf, crc32Stream.Crc);
                    await _countingWritableSubStream.WriteAsync(intBuf, 0, 4);

                    BinaryPrimitives.WriteInt64LittleEndian(intBuf, _writeCount);
                    await _countingWritableSubStream.WriteAsync(intBuf, 0, 8);

                    //total with headers
                    BinaryPrimitives.WriteUInt64LittleEndian(intBuf, compressedCount + 6 + 20);
                    await _countingWritableSubStream.WriteAsync(intBuf, 0, 8);
                }
                _finished = true;
            }
        }

        #region Stream methods

        public override async ValueTask DisposeAsync()
        {
            if (_disposed)
            {
                return;
            }
            _disposed = true;
                await FinishAsync();
                await _stream.DisposeAsync();
        }

        public CompressionMode Mode { get; private set; }

        public override bool CanRead => Mode == CompressionMode.Decompress;

        public override bool CanSeek => false;

        public override bool CanWrite => Mode == CompressionMode.Compress;

        public override Task FlushAsync(CancellationToken cancellationToken)
        {
            return _stream.FlushAsync(cancellationToken);
        }

        // TODO: Both Length and Position are sometimes feasible, but would require
        // reading the output length when we initialize.
        public override long Length => throw new NotSupportedException();

        public override long Position { get => throw new NotImplementedException(); set => throw new NotSupportedException(); }

        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = new CancellationToken())
        {
            return _stream.ReadAsync(buffer, cancellationToken);
        }

        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

        public override void SetLength(long value) => throw new NotSupportedException();

        public override async ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = new CancellationToken())
        {
            await _stream.WriteAsync(buffer, cancellationToken);
            _writeCount += buffer.Length;
        }

        public override async Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
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
        public static async ValueTask<bool> IsLZipFileAsync(Stream stream) => await ValidateAndReadSize(stream) != 0;

        /// <summary>
        /// Reads the 6-byte header of the stream, and returns 0 if either the header
        /// couldn't be read or it isn't a validate LZIP header, or the dictionary
        /// size if it *is* a valid LZIP file.
        /// </summary>
        private static async ValueTask<int> ValidateAndReadSize(Stream stream)
        {
            if (stream is null)
            {
                throw new ArgumentNullException(nameof(stream));
            }

            // Read the header
            using var buffer = MemoryPool<byte>.Shared.Rent(6);
            var header = buffer.Memory.Slice(0,6);
            int n = await stream.ReadAsync(header);

            // TODO: Handle reading only part of the header?

            if (n != 6)
            {
                return 0;
            }

            if (header.Span[0] != 'L' || header.Span[1] != 'Z' || header.Span[2] != 'I' || header.Span[3] != 'P' || header.Span[4] != 1 /* version 1 */)
            {
                return 0;
            }
            int basePower = header.Span[5] & 0x1F;
            int subtractionNumerator = (header.Span[5] & 0xE0) >> 5;
            return (1 << basePower) - subtractionNumerator * (1 << (basePower - 4));
        }

        private static readonly byte[] headerBytes = new byte[6] { (byte)'L', (byte)'Z', (byte)'I', (byte)'P', 1, 113 };

        public static async ValueTask WriteHeaderSizeAsync(Stream stream)
        {
            if (stream is null)
            {
                throw new ArgumentNullException(nameof(stream));
            }

            // hard coding the dictionary size encoding
            await stream.WriteAsync(headerBytes, 0, 6);
        }

        /// <summary>
        /// Creates a byte array to communicate the parameters and dictionary size to LzmaStream.
        /// </summary>
        private static byte[] GetProperties(int dictionarySize) =>
            new byte[]
            {
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
                (byte)((dictionarySize >> 24) & 0xff)
            };
    }
}
