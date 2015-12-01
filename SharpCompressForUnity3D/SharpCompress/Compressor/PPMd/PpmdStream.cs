namespace SharpCompress.Compressor.PPMd
{
    using SharpCompress.Compressor.LZMA.RangeCoder;
    using SharpCompress.Compressor.PPMd.H;
    using SharpCompress.Compressor.PPMd.I1;
    using System;
    using System.IO;

    public class PpmdStream : Stream
    {
        private bool compress;
        private Decoder decoder;
        private bool isDisposed;
        private Model model;
        private ModelPPM modelH;
        private long position = 0L;
        private PpmdProperties properties;
        private Stream stream;

        public PpmdStream(PpmdProperties properties, Stream stream, bool compress)
        {
            this.properties = properties;
            this.stream = stream;
            this.compress = compress;
            if (properties.Version == PpmdVersion.I1)
            {
                this.model = new Model();
                if (compress)
                {
                    this.model.EncodeStart(properties);
                }
                else
                {
                    this.model.DecodeStart(stream, properties);
                }
            }
            if (properties.Version == PpmdVersion.H)
            {
                this.modelH = new ModelPPM();
                if (compress)
                {
                    throw new NotImplementedException();
                }
                this.modelH.decodeInit(stream, properties.ModelOrder, properties.AllocatorSize);
            }
            if (properties.Version == PpmdVersion.H7z)
            {
                this.modelH = new ModelPPM();
                if (compress)
                {
                    throw new NotImplementedException();
                }
                this.modelH.decodeInit(null, properties.ModelOrder, properties.AllocatorSize);
                this.decoder = new Decoder();
                this.decoder.Init(stream);
            }
        }

        protected override void Dispose(bool isDisposing)
        {
            if (!this.isDisposed)
            {
                this.isDisposed = true;
                if (isDisposing && this.compress)
                {
                    this.model.EncodeBlock(this.stream, new MemoryStream(), true);
                }
                base.Dispose(isDisposing);
            }
        }

        public override void Flush()
        {
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            int num2;
            if (this.compress)
            {
                return 0;
            }
            int num = 0;
            if (this.properties.Version == PpmdVersion.I1)
            {
                num = this.model.DecodeBlock(this.stream, buffer, offset, count);
            }
            if (this.properties.Version == PpmdVersion.H)
            {
                while ((num < count) && ((num2 = this.modelH.decodeChar()) >= 0))
                {
                    buffer[offset++] = (byte) num2;
                    num++;
                }
            }
            if (this.properties.Version == PpmdVersion.H7z)
            {
                while ((num < count) && ((num2 = this.modelH.decodeChar(this.decoder)) >= 0))
                {
                    buffer[offset++] = (byte) num2;
                    num++;
                }
            }
            this.position += num;
            return num;
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            throw new NotSupportedException();
        }

        public override void SetLength(long value)
        {
            throw new NotSupportedException();
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            if (this.compress)
            {
                this.model.EncodeBlock(this.stream, new MemoryStream(buffer, offset, count), false);
            }
        }

        public override bool CanRead
        {
            get
            {
                return !this.compress;
            }
        }

        public override bool CanSeek
        {
            get
            {
                return false;
            }
        }

        public override bool CanWrite
        {
            get
            {
                return this.compress;
            }
        }

        public override long Length
        {
            get
            {
                throw new NotSupportedException();
            }
        }

        public override long Position
        {
            get
            {
                return this.position;
            }
            set
            {
                throw new NotSupportedException();
            }
        }
    }
}

