namespace SharpCompress.Compressor.BZip2
{
    using SharpCompress.Compressor;
    using System;
    using System.IO;
    using System.Runtime.CompilerServices;
    using System.Runtime.InteropServices;

    public class BZip2Stream : Stream
    {
        [CompilerGenerated]
        private CompressionMode _Mode_k__BackingField;
        private bool isDisposed;
        private readonly Stream stream;

        public BZip2Stream(Stream stream, CompressionMode compressionMode, [Optional, DefaultParameterValue(false)] bool leaveOpen, [Optional, DefaultParameterValue(false)] bool decompressContacted)
        {
            this.Mode = compressionMode;
            if (this.Mode == CompressionMode.Compress)
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
            if (!this.isDisposed)
            {
                this.isDisposed = true;
                if (disposing)
                {
                    this.stream.Dispose();
                }
            }
        }

        public override void Flush()
        {
            this.stream.Flush();
        }

        public static bool IsBZip2(Stream stream)
        {
            byte[] buffer = new BinaryReader(stream).ReadBytes(2);
            if (((buffer.Length < 2) || (buffer[0] != 0x42)) || (buffer[1] != 90))
            {
                return false;
            }
            return true;
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            return this.stream.Read(buffer, offset, count);
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            return this.stream.Seek(offset, origin);
        }

        public override void SetLength(long value)
        {
            this.stream.SetLength(value);
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            this.stream.Write(buffer, offset, count);
        }

        public override bool CanRead
        {
            get
            {
                return this.stream.CanRead;
            }
        }

        public override bool CanSeek
        {
            get
            {
                return this.stream.CanSeek;
            }
        }

        public override bool CanWrite
        {
            get
            {
                return this.stream.CanWrite;
            }
        }

        public override long Length
        {
            get
            {
                return this.stream.Length;
            }
        }

        public CompressionMode Mode
        {
            [CompilerGenerated]
            get
            {
                return this._Mode_k__BackingField;
            }
            [CompilerGenerated]
            private set
            {
                this._Mode_k__BackingField = value;
            }
        }

        public override long Position
        {
            get
            {
                return this.stream.Position;
            }
            set
            {
                this.stream.Position = value;
            }
        }
    }
}

