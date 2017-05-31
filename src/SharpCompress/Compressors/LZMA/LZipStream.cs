using System;
using System.IO;
using SharpCompress.Converters;
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
    public class LZipStream : Stream
    {
        private readonly Stream stream;
        private readonly CountingWritableSubStream rawStream;
        private bool disposed;
        private readonly bool leaveOpen;
        private bool finished;

        private long writeCount;

        public LZipStream(Stream stream, CompressionMode mode, bool leaveOpen = false)
        {
            Mode = mode;
            this.leaveOpen = leaveOpen;

            if (mode == CompressionMode.Decompress)
            {
                int dSize = ValidateAndReadSize(stream);
                if (dSize == 0)
                {
                    throw new IOException("Not an LZip stream");
                }
                byte[] properties = GetProperties(dSize);
                this.stream = new LzmaStream(properties, stream);
            }
            else
            {
                //default
                int dSize = 104 * 1024;
                WriteHeaderSize(stream);

                rawStream = new CountingWritableSubStream(stream);
                this.stream = new Crc32Stream(new LzmaStream(new LzmaEncoderProperties(true, dSize), false, rawStream));
            }
        }

        public void Finish()
        {
            if (!finished)
            {
                if (Mode == CompressionMode.Compress)
                {
                    var crc32Stream = (Crc32Stream)stream;
                    crc32Stream.WrappedStream.Dispose();
                    crc32Stream.Dispose();
                    var compressedCount = rawStream.Count;
                    
                    var bytes = DataConverter.LittleEndian.GetBytes(crc32Stream.Crc);
                    rawStream.Write(bytes, 0, bytes.Length);

                    bytes = DataConverter.LittleEndian.GetBytes(writeCount);
                    rawStream.Write(bytes, 0, bytes.Length);

                    //total with headers
                    bytes = DataConverter.LittleEndian.GetBytes(compressedCount + 6 + 20);
                    rawStream.Write(bytes, 0, bytes.Length);
                }
                finished = true;
            }
        }

        #region Stream methods

        protected override void Dispose(bool disposing)
        {
            if (disposed)
            {
                return;
            }
            disposed = true;
            if (disposing)
            {
                Finish();
                if (!leaveOpen)
                {
                    rawStream.Dispose();
                }
            }
        }

        public CompressionMode Mode { get; }

        public override bool CanRead => Mode == CompressionMode.Decompress;

        public override bool CanSeek => false;

        public override bool CanWrite => Mode == CompressionMode.Compress;

        public override void Flush()
        {
            stream.Flush();
        }
    
        // TODO: Both Length and Position are sometimes feasible, but would require
        // reading the output length when we initialize.
        public override long Length => throw new NotImplementedException();

        public override long Position { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }

        public override int Read(byte[] buffer, int offset, int count) => stream.Read(buffer, offset, count);

        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

        public override void SetLength(long value) => throw new NotImplementedException();

        public override void Write(byte[] buffer, int offset, int count)
        {
            stream.Write(buffer, offset, count);
            writeCount += count;
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
            if (stream == null)
            {
                throw new ArgumentNullException(nameof(stream));
            }
            // Read the header
            byte[] header = new byte[6];
            int n = stream.Read(header, 0, header.Length);

            // TODO: Handle reading only part of the header?

            if (n != 6)
            {
                return 0;
            }

            if (header[0] != 'L' || header[1] != 'Z' || header[2] != 'I' || header[3] != 'P' || header[4] != 1 /* version 1 */)
            {
                return 0;
            }
            int basePower = header[5] & 0x1F;
            int subtractionNumerator = (header[5] & 0xE0) >> 5;
            return (1 << basePower) - subtractionNumerator * (1 << (basePower - 4));
        }

        public static void WriteHeaderSize(Stream stream)
        {
            if (stream == null)
            {
                throw new ArgumentNullException(nameof(stream));
            }
            // hard coding the dictionary size encoding
            byte[] header = new byte[6] {(byte)'L', (byte)'Z', (byte)'I', (byte)'P', 1, 113};
            stream.Write(header, 0, 6);
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
