namespace SharpCompress.Compressor.Deflate
{
    using SharpCompress.Common;
    using SharpCompress.Common.Tar.Headers;
    using SharpCompress.Compressor;
    using System;
    using System.Collections.Generic;
    using System.IO;

    internal class ZlibBaseStream : Stream
    {
        protected internal byte[] _buf1 = new byte[1];
        protected internal int _bufferSize = 0x4000;
        protected internal CompressionMode _compressionMode;
        protected internal ZlibStreamFlavor _flavor;
        protected internal FlushType _flushMode = FlushType.None;
        protected internal string _GzipComment;
        protected internal string _GzipFileName;
        protected internal int _gzipHeaderByteCount;
        protected internal DateTime _GzipMtime;
        protected internal bool _leaveOpen;
        protected internal CompressionLevel _level;
        protected internal Stream _stream;
        protected internal StreamMode _streamMode = StreamMode.Undefined;
        protected internal byte[] _workingBuffer;
        protected internal ZlibCodec _z = null;
        private CRC32 crc;
        private bool isDisposed;
        private bool nomoreinput = false;
        protected internal CompressionStrategy Strategy = CompressionStrategy.Default;

        public ZlibBaseStream(Stream stream, CompressionMode compressionMode, CompressionLevel level, ZlibStreamFlavor flavor, bool leaveOpen)
        {
            this._stream = stream;
            this._leaveOpen = leaveOpen;
            this._compressionMode = compressionMode;
            this._flavor = flavor;
            this._level = level;
            if (flavor == ZlibStreamFlavor.GZIP)
            {
                this.crc = new CRC32();
            }
        }

        private int _ReadAndValidateGzipHeader()
        {
            int num = 0;
            byte[] buffer = new byte[10];
            int num2 = this._stream.Read(buffer, 0, buffer.Length);
            if (num2 == 0)
            {
                return 0;
            }
            if (num2 != 10)
            {
                throw new ZlibException("Not a valid GZIP stream.");
            }
            if (((buffer[0] != 0x1f) || (buffer[1] != 0x8b)) || (buffer[2] != 8))
            {
                throw new ZlibException("Bad GZIP header.");
            }
            int num3 = BitConverter.ToInt32(buffer, 4);
            this._GzipMtime = TarHeader.Epoch.AddSeconds((double) num3);
            num += num2;
            if ((buffer[3] & 4) == 4)
            {
                num2 = this._stream.Read(buffer, 0, 2);
                num += num2;
                short num4 = (short) (buffer[0] + (buffer[1] * 0x100));
                byte[] buffer2 = new byte[num4];
                num2 = this._stream.Read(buffer2, 0, buffer2.Length);
                if (num2 != num4)
                {
                    throw new ZlibException("Unexpected end-of-file reading GZIP header.");
                }
                num += num2;
            }
            if ((buffer[3] & 8) == 8)
            {
                this._GzipFileName = this.ReadZeroTerminatedString();
            }
            if ((buffer[3] & 0x10) == 0x10)
            {
                this._GzipComment = this.ReadZeroTerminatedString();
            }
            if ((buffer[3] & 2) == 2)
            {
                this.Read(this._buf1, 0, 1);
            }
            return num;
        }

        protected override void Dispose(bool disposing)
        {
            if (!this.isDisposed)
            {
                this.isDisposed = true;
                base.Dispose(disposing);
                if (disposing && (this._stream != null))
                {
                    try
                    {
                        this.finish();
                    }
                    finally
                    {
                        this.end();
                        if (!this._leaveOpen)
                        {
                            this._stream.Dispose();
                        }
                        this._stream = null;
                    }
                }
            }
        }

        private void end()
        {
            if (this.z != null)
            {
                if (this._wantCompress)
                {
                    this._z.EndDeflate();
                }
                else
                {
                    this._z.EndInflate();
                }
                this._z = null;
            }
        }

        private void finish()
        {
            if (this._z != null)
            {
                if (this._streamMode == StreamMode.Writer)
                {
                    bool flag = false;
                    do
                    {
                        this._z.OutputBuffer = this.workingBuffer;
                        this._z.NextOut = 0;
                        this._z.AvailableBytesOut = this._workingBuffer.Length;
                        int num = this._wantCompress ? this._z.Deflate(FlushType.Finish) : this._z.Inflate(FlushType.Finish);
                        if ((num != 1) && (num != 0))
                        {
                            string str = (this._wantCompress ? "de" : "in") + "flating";
                            if (this._z.Message == null)
                            {
                                throw new ZlibException(string.Format("{0}: (rc = {1})", str, num));
                            }
                            throw new ZlibException(str + ": " + this._z.Message);
                        }
                        if ((this._workingBuffer.Length - this._z.AvailableBytesOut) > 0)
                        {
                            this._stream.Write(this._workingBuffer, 0, this._workingBuffer.Length - this._z.AvailableBytesOut);
                        }
                        flag = (this._z.AvailableBytesIn == 0) && (this._z.AvailableBytesOut != 0);
                        if (!((this._flavor != ZlibStreamFlavor.GZIP) || this._wantCompress))
                        {
                            flag = (this._z.AvailableBytesIn == 8) && (this._z.AvailableBytesOut != 0);
                        }
                    }
                    while (!flag);
                    this.Flush();
                    if (this._flavor == ZlibStreamFlavor.GZIP)
                    {
                        if (!this._wantCompress)
                        {
                            throw new ZlibException("Writing with decompression is not supported.");
                        }
                        int num2 = this.crc.Crc32Result;
                        this._stream.Write(BitConverter.GetBytes(num2), 0, 4);
                        int num3 = (int) (((ulong) this.crc.TotalBytesRead) & 0xffffffffL);
                        this._stream.Write(BitConverter.GetBytes(num3), 0, 4);
                    }
                }
                else if ((this._streamMode == StreamMode.Reader) && (this._flavor == ZlibStreamFlavor.GZIP))
                {
                    if (this._wantCompress)
                    {
                        throw new ZlibException("Reading with compression is not supported.");
                    }
                    if (this._z.TotalBytesOut != 0L)
                    {
                        byte[] destinationArray = new byte[8];
                        if (this._z.AvailableBytesIn != 8)
                        {
                            Array.Copy(this._z.InputBuffer, this._z.NextIn, destinationArray, 0, this._z.AvailableBytesIn);
                            int count = 8 - this._z.AvailableBytesIn;
                            int num5 = this._stream.Read(destinationArray, this._z.AvailableBytesIn, count);
                            if (count != num5)
                            {
                                throw new ZlibException(string.Format("Protocol error. AvailableBytesIn={0}, expected 8", this._z.AvailableBytesIn + num5));
                            }
                        }
                        else
                        {
                            Array.Copy(this._z.InputBuffer, this._z.NextIn, destinationArray, 0, destinationArray.Length);
                        }
                        int num6 = BitConverter.ToInt32(destinationArray, 0);
                        int num7 = this.crc.Crc32Result;
                        int num8 = BitConverter.ToInt32(destinationArray, 4);
                        int num9 = (int) (((ulong) this._z.TotalBytesOut) & 0xffffffffL);
                        if (num7 != num6)
                        {
                            throw new ZlibException(string.Format("Bad CRC32 in GZIP stream. (actual({0:X8})!=expected({1:X8}))", num7, num6));
                        }
                        if (num9 != num8)
                        {
                            throw new ZlibException(string.Format("Bad size in GZIP stream. (actual({0})!=expected({1}))", num9, num8));
                        }
                    }
                }
            }
        }

        public override void Flush()
        {
            this._stream.Flush();
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            if (this._streamMode == StreamMode.Undefined)
            {
                if (!this._stream.CanRead)
                {
                    throw new ZlibException("The stream is not readable.");
                }
                this._streamMode = StreamMode.Reader;
                this.z.AvailableBytesIn = 0;
                if (this._flavor == ZlibStreamFlavor.GZIP)
                {
                    this._gzipHeaderByteCount = this._ReadAndValidateGzipHeader();
                    if (this._gzipHeaderByteCount == 0)
                    {
                        return 0;
                    }
                }
            }
            if (this._streamMode != StreamMode.Reader)
            {
                throw new ZlibException("Cannot Read after Writing.");
            }
            if (count == 0)
            {
                return 0;
            }
            if (this.nomoreinput && this._wantCompress)
            {
                return 0;
            }
            if (buffer == null)
            {
                throw new ArgumentNullException("buffer");
            }
            if (count < 0)
            {
                throw new ArgumentOutOfRangeException("count");
            }
            if (offset < buffer.GetLowerBound(0))
            {
                throw new ArgumentOutOfRangeException("offset");
            }
            if ((offset + count) > buffer.GetLength(0))
            {
                throw new ArgumentOutOfRangeException("count");
            }
            int num = 0;
            this._z.OutputBuffer = buffer;
            this._z.NextOut = offset;
            this._z.AvailableBytesOut = count;
            this._z.InputBuffer = this.workingBuffer;
            do
            {
                if ((this._z.AvailableBytesIn == 0) && !this.nomoreinput)
                {
                    this._z.NextIn = 0;
                    this._z.AvailableBytesIn = this._stream.Read(this._workingBuffer, 0, this._workingBuffer.Length);
                    if (this._z.AvailableBytesIn == 0)
                    {
                        this.nomoreinput = true;
                    }
                }
                num = this._wantCompress ? this._z.Deflate(this._flushMode) : this._z.Inflate(this._flushMode);
                if (this.nomoreinput && (num == -5))
                {
                    return 0;
                }
                if ((num != 0) && (num != 1))
                {
                    throw new ZlibException(string.Format("{0}flating:  rc={1}  msg={2}", this._wantCompress ? "de" : "in", num, this._z.Message));
                }
            }
            while (((!this.nomoreinput && (num != 1)) || (this._z.AvailableBytesOut != count)) && (((this._z.AvailableBytesOut > 0) && !this.nomoreinput) && (num == 0)));
            if (this._z.AvailableBytesOut > 0)
            {
                if ((num == 0) && (this._z.AvailableBytesIn == 0))
                {
                }
                if (this.nomoreinput && this._wantCompress)
                {
                    num = this._z.Deflate(FlushType.Finish);
                    if ((num != 0) && (num != 1))
                    {
                        throw new ZlibException(string.Format("Deflating:  rc={0}  msg={1}", num, this._z.Message));
                    }
                }
            }
            num = count - this._z.AvailableBytesOut;
            if (this.crc != null)
            {
                this.crc.SlurpBlock(buffer, offset, num);
            }
            return num;
        }

        private string ReadZeroTerminatedString()
        {
            List<byte> list = new List<byte>();
            bool flag = false;
            do
            {
                if (this._stream.Read(this._buf1, 0, 1) != 1)
                {
                    throw new ZlibException("Unexpected EOF reading GZIP header.");
                }
                if (this._buf1[0] == 0)
                {
                    flag = true;
                }
                else
                {
                    list.Add(this._buf1[0]);
                }
            }
            while (!flag);
            byte[] bytes = list.ToArray();
            return ArchiveEncoding.Default.GetString(bytes, 0, bytes.Length);
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            throw new NotSupportedException();
        }

        public override void SetLength(long value)
        {
            this._stream.SetLength(value);
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            if (this.crc != null)
            {
                this.crc.SlurpBlock(buffer, offset, count);
            }
            if (this._streamMode == StreamMode.Undefined)
            {
                this._streamMode = StreamMode.Writer;
            }
            else if (this._streamMode != StreamMode.Writer)
            {
                throw new ZlibException("Cannot Write after Reading.");
            }
            if (count != 0)
            {
                this.z.InputBuffer = buffer;
                this._z.NextIn = offset;
                this._z.AvailableBytesIn = count;
                bool flag = false;
                do
                {
                    this._z.OutputBuffer = this.workingBuffer;
                    this._z.NextOut = 0;
                    this._z.AvailableBytesOut = this._workingBuffer.Length;
                    int num = this._wantCompress ? this._z.Deflate(this._flushMode) : this._z.Inflate(this._flushMode);
                    if ((num != 0) && (num != 1))
                    {
                        throw new ZlibException((this._wantCompress ? "de" : "in") + "flating: " + this._z.Message);
                    }
                    this._stream.Write(this._workingBuffer, 0, this._workingBuffer.Length - this._z.AvailableBytesOut);
                    flag = (this._z.AvailableBytesIn == 0) && (this._z.AvailableBytesOut != 0);
                    if (!((this._flavor != ZlibStreamFlavor.GZIP) || this._wantCompress))
                    {
                        flag = (this._z.AvailableBytesIn == 8) && (this._z.AvailableBytesOut != 0);
                    }
                }
                while (!flag);
            }
        }

        protected internal bool _wantCompress
        {
            get
            {
                return (this._compressionMode == CompressionMode.Compress);
            }
        }

        public override bool CanRead
        {
            get
            {
                return this._stream.CanRead;
            }
        }

        public override bool CanSeek
        {
            get
            {
                return this._stream.CanSeek;
            }
        }

        public override bool CanWrite
        {
            get
            {
                return this._stream.CanWrite;
            }
        }

        internal int Crc32
        {
            get
            {
                if (this.crc == null)
                {
                    return 0;
                }
                return this.crc.Crc32Result;
            }
        }

        public override long Length
        {
            get
            {
                return this._stream.Length;
            }
        }

        public override long Position
        {
            get
            {
                throw new NotSupportedException();
            }
            set
            {
                throw new NotSupportedException();
            }
        }

        private byte[] workingBuffer
        {
            get
            {
                if (this._workingBuffer == null)
                {
                    this._workingBuffer = new byte[this._bufferSize];
                }
                return this._workingBuffer;
            }
        }

        private ZlibCodec z
        {
            get
            {
                if (this._z == null)
                {
                    bool flag = this._flavor == ZlibStreamFlavor.ZLIB;
                    this._z = new ZlibCodec();
                    if (this._compressionMode == CompressionMode.Decompress)
                    {
                        this._z.InitializeInflate(flag);
                    }
                    else
                    {
                        this._z.Strategy = this.Strategy;
                        this._z.InitializeDeflate(this._level, flag);
                    }
                }
                return this._z;
            }
        }

        internal enum StreamMode
        {
            Writer,
            Reader,
            Undefined
        }
    }
}

