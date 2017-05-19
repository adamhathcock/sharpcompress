using System;
using System.IO;

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
        private bool disposed;
        private readonly bool leaveOpen;

        public LZipStream(Stream stream, CompressionMode mode)
            : this(stream, mode, false)
        {
        }

        public LZipStream(Stream stream, CompressionMode mode, bool leaveOpen)
        {
            if (mode != CompressionMode.Decompress)
            {
                throw new NotImplementedException("Only LZip decompression is currently supported");
            }
            Mode = mode;
            this.leaveOpen = leaveOpen;
            int dictionarySize = ValidateAndReadSize(stream);
            if (dictionarySize == 0)
            {
                throw new IOException("Not an LZip stream");
            }
            byte[] properties = GetProperties(dictionarySize);
            this.stream = new LzmaStream(properties, stream);
        }

        #region Stream methods

        protected override void Dispose(bool disposing)
        {
            if (disposed)
            {
                return;
            }
            disposed = true;
            if (disposing && !leaveOpen)
            {
                stream.Dispose();
            }
        }

        public CompressionMode Mode { get; }

        public override bool CanRead => stream.CanRead;

        public override bool CanSeek => false;

        public override bool CanWrite => false;

        public override void Flush()
        {
            stream.Flush();
        }
    
        // TODO: Both Length and Position are sometimes feasible, but would require
        // reading the output length when we initialize.
        public override long Length => throw new NotImplementedException();

        public override long Position { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }

        public override int Read(byte[] buffer, int offset, int count) => stream.Read(buffer, offset, count);

        public override long Seek(long offset, SeekOrigin origin)
        {
            throw new NotSupportedException();
        }

        public override void SetLength(long value)
        {
            throw new NotImplementedException();
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            throw new NotImplementedException();
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
        private static int ValidateAndReadSize(Stream stream)
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
