namespace SharpCompress.Compressor.LZMA
{
    using SharpCompress.Compressor.LZMA.LZ;
    using SharpCompress.Compressor.LZMA.RangeCoder;
    using System;
    using System.IO;

    public class LzmaStream : Stream
    {
        private long availableBytes;
        private SharpCompress.Compressor.LZMA.Decoder decoder;
        private int dictionarySize;
        private SharpCompress.Compressor.LZMA.Encoder encoder;
        private bool endReached;
        private long inputPosition;
        private long inputSize;
        private Stream inputStream;
        private bool isDisposed;
        private bool isLZMA2;
        private bool needDictReset;
        private bool needProps;
        private long outputSize;
        private OutWindow outWindow;
        private long position;
        private byte[] props;
        private SharpCompress.Compressor.LZMA.RangeCoder.Decoder rangeDecoder;
        private long rangeDecoderLimit;
        private bool uncompressedChunk;

        public LzmaStream(byte[] properties, Stream inputStream) : this(properties, inputStream, -1L, -1L, null, properties.Length < 5)
        {
        }

        public LzmaStream(LzmaEncoderProperties properties, bool isLZMA2, Stream outputStream) : this(properties, isLZMA2, null, outputStream)
        {
        }

        public LzmaStream(byte[] properties, Stream inputStream, long inputSize) : this(properties, inputStream, inputSize, -1L, null, properties.Length < 5)
        {
        }

        public LzmaStream(LzmaEncoderProperties properties, bool isLZMA2, Stream presetDictionary, Stream outputStream)
        {
            this.outWindow = new OutWindow();
            this.rangeDecoder = new SharpCompress.Compressor.LZMA.RangeCoder.Decoder();
            this.position = 0L;
            this.endReached = false;
            this.inputPosition = 0L;
            this.uncompressedChunk = false;
            this.needDictReset = true;
            this.needProps = true;
            this.props = new byte[5];
            this.isLZMA2 = isLZMA2;
            this.availableBytes = 0L;
            this.endReached = true;
            if (isLZMA2)
            {
                throw new NotImplementedException();
            }
            this.encoder = new SharpCompress.Compressor.LZMA.Encoder();
            this.encoder.SetCoderProperties(properties.propIDs, properties.properties);
            MemoryStream outStream = new MemoryStream(5);
            this.encoder.WriteCoderProperties(outStream);
            this.props = outStream.ToArray();
            this.encoder.SetStreams(null, outputStream, -1L, -1L);
            if (presetDictionary != null)
            {
                this.encoder.Train(presetDictionary);
            }
        }

        public LzmaStream(byte[] properties, Stream inputStream, long inputSize, long outputSize) : this(properties, inputStream, inputSize, outputSize, null, properties.Length < 5)
        {
        }

        public LzmaStream(byte[] properties, Stream inputStream, long inputSize, long outputSize, Stream presetDictionary, bool isLZMA2)
        {
            this.outWindow = new OutWindow();
            this.rangeDecoder = new SharpCompress.Compressor.LZMA.RangeCoder.Decoder();
            this.position = 0L;
            this.endReached = false;
            this.inputPosition = 0L;
            this.uncompressedChunk = false;
            this.needDictReset = true;
            this.needProps = true;
            this.props = new byte[5];
            this.inputStream = inputStream;
            this.inputSize = inputSize;
            this.outputSize = outputSize;
            this.isLZMA2 = isLZMA2;
            if (!isLZMA2)
            {
                this.dictionarySize = BitConverter.ToInt32(properties, 1);
                this.outWindow.Create(this.dictionarySize);
                if (presetDictionary != null)
                {
                    this.outWindow.Train(presetDictionary);
                }
                this.rangeDecoder.Init(inputStream);
                this.decoder = new SharpCompress.Compressor.LZMA.Decoder();
                this.decoder.SetDecoderProperties(properties);
                this.props = properties;
                this.availableBytes = (outputSize < 0L) ? 0x7fffffffffffffffL : outputSize;
                this.rangeDecoderLimit = inputSize;
            }
            else
            {
                this.dictionarySize = 2 | (properties[0] & 1);
                this.dictionarySize = this.dictionarySize << ((properties[0] >> 1) + 11);
                this.outWindow.Create(this.dictionarySize);
                if (presetDictionary != null)
                {
                    this.outWindow.Train(presetDictionary);
                    this.needDictReset = false;
                }
                this.props = new byte[1];
                this.availableBytes = 0L;
            }
        }

        private void decodeChunkHeader()
        {
            int num = this.inputStream.ReadByte();
            this.inputPosition += 1L;
            if (num == 0)
            {
                this.endReached = true;
            }
            else
            {
                if ((num >= 0xe0) || (num == 1))
                {
                    this.needProps = true;
                    this.needDictReset = false;
                    this.outWindow.Reset();
                }
                else if (this.needDictReset)
                {
                    throw new DataErrorException();
                }
                if (num >= 0x80)
                {
                    this.uncompressedChunk = false;
                    this.availableBytes = (num & 0x1f) << 0x10;
                    this.availableBytes += ((this.inputStream.ReadByte() << 8) + this.inputStream.ReadByte()) + 1;
                    this.inputPosition += 2L;
                    this.rangeDecoderLimit = ((this.inputStream.ReadByte() << 8) + this.inputStream.ReadByte()) + 1;
                    this.inputPosition += 2L;
                    if (num >= 0xc0)
                    {
                        this.needProps = false;
                        this.props[0] = (byte) this.inputStream.ReadByte();
                        this.inputPosition += 1L;
                        this.decoder = new SharpCompress.Compressor.LZMA.Decoder();
                        this.decoder.SetDecoderProperties(this.props);
                    }
                    else
                    {
                        if (this.needProps)
                        {
                            throw new DataErrorException();
                        }
                        if (num >= 160)
                        {
                            this.decoder = new SharpCompress.Compressor.LZMA.Decoder();
                            this.decoder.SetDecoderProperties(this.props);
                        }
                    }
                    this.rangeDecoder.Init(this.inputStream);
                }
                else
                {
                    if (num > 2)
                    {
                        throw new DataErrorException();
                    }
                    this.uncompressedChunk = true;
                    this.availableBytes = ((this.inputStream.ReadByte() << 8) + this.inputStream.ReadByte()) + 1;
                    this.inputPosition += 2L;
                }
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (!this.isDisposed)
            {
                this.isDisposed = true;
                if (disposing)
                {
                    if (this.encoder != null)
                    {
                        this.position = this.encoder.Code(null, true);
                    }
                    if (this.inputStream != null)
                    {
                        this.inputStream.Dispose();
                    }
                }
                base.Dispose(disposing);
            }
        }

        public override void Flush()
        {
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            if (this.endReached)
            {
                return 0;
            }
            int num = 0;
            while (num < count)
            {
                if (this.availableBytes == 0L)
                {
                    if (this.isLZMA2)
                    {
                        this.decodeChunkHeader();
                    }
                    else
                    {
                        this.endReached = true;
                    }
                    if (this.endReached)
                    {
                        break;
                    }
                }
                int len = count - num;
                if (len > this.availableBytes)
                {
                    len = (int) this.availableBytes;
                }
                this.outWindow.SetLimit((long) len);
                if (this.uncompressedChunk)
                {
                    this.inputPosition += this.outWindow.CopyStream(this.inputStream, len);
                }
                else if (this.decoder.Code(this.dictionarySize, this.outWindow, this.rangeDecoder) && (this.outputSize < 0L))
                {
                    this.availableBytes = this.outWindow.AvailableBytes;
                }
                int num3 = this.outWindow.Read(buffer, offset, len);
                num += num3;
                offset += num3;
                this.position += num3;
                this.availableBytes -= num3;
                if ((this.availableBytes == 0L) && !this.uncompressedChunk)
                {
                    this.rangeDecoder.ReleaseStream();
                    if (!(this.rangeDecoder.IsFinished && ((this.rangeDecoderLimit < 0L) || (this.rangeDecoder.Total == this.rangeDecoderLimit))))
                    {
                        throw new DataErrorException();
                    }
                    this.inputPosition += this.rangeDecoder.Total;
                    if (this.outWindow.HasPending)
                    {
                        throw new DataErrorException();
                    }
                }
            }
            if (this.endReached)
            {
                if ((this.inputSize >= 0L) && (this.inputPosition != this.inputSize))
                {
                    throw new DataErrorException();
                }
                if ((this.outputSize >= 0L) && (this.position != this.outputSize))
                {
                    throw new DataErrorException();
                }
            }
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
            if (this.encoder != null)
            {
                this.position = this.encoder.Code(new MemoryStream(buffer, offset, count), false);
            }
        }

        public override bool CanRead
        {
            get
            {
                return (this.encoder == null);
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
                return (this.encoder != null);
            }
        }

        public override long Length
        {
            get
            {
                return (this.position + this.availableBytes);
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

        public byte[] Properties
        {
            get
            {
                return this.props;
            }
        }
    }
}

