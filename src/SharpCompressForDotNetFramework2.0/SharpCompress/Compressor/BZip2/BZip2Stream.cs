using System.IO;

namespace SharpCompress.Compressor.BZip2
{
    public class BZip2Stream : Stream
    {
        private readonly Stream stream;

        /// <summary>
        /// Create a BZip2Stream
        /// </summary>
        /// <param name="stream">The stream to read from</param>
        /// <param name="compressionMode">Compression Mode</param>
        /// <param name="leaveOpen">Leave the underlying stream open when disposed.</param>
        /// <param name="decompressContacted">Should the BZip2 stream continue to decompress the stream when the End Marker is found.</param>
        public BZip2Stream(Stream stream, CompressionMode compressionMode, bool leaveOpen = false, bool decompressContacted = false)
        {
            Mode = compressionMode;
            if (Mode == CompressionMode.Compress)
            {
                this.stream = new CBZip2OutputStream(stream, leaveOpen);
            }
            else
            {
                this.stream = new CBZip2InputStream(stream, decompressContacted, leaveOpen);
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                stream.Dispose();
            }
        }

        public CompressionMode Mode { get; private set; }

        public override bool CanRead
        {
            get { return stream.CanRead; }
        }

        public override bool CanSeek
        {
            get { return stream.CanSeek; }
        }

        public override bool CanWrite
        {
            get { return stream.CanWrite; }
        }

        public override void Flush()
        {
            stream.Flush();
        }

        public override long Length
        {
            get { return stream.Length; }
        }

        public override long Position
        {
            get { return stream.Position; }
            set { stream.Position = value; }
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            return stream.Read(buffer, offset, count);
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            return stream.Seek(offset, origin);
        }

        public override void SetLength(long value)
        {
            stream.SetLength(value);
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            stream.Write(buffer, offset, count);
        }

        /// <summary>
        /// Consumes two bytes to test if there is a BZip2 header
        /// </summary>
        /// <param name="stream"></param>
        /// <returns></returns>
        public static bool IsBZip2(Stream stream)
        {
            BinaryReader br = new BinaryReader(stream);
            byte[] chars = br.ReadBytes(2);
            if (chars.Length < 2 || chars[0] != 'B' || chars[1] != 'Z')
            {
                return false;
            }
            return true;
        }
    }
}
