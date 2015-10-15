namespace SharpCompress.Compressor.Deflate
{
    using SharpCompress.Compressor;
    using System;

    internal sealed class ZlibCodec
    {
        internal uint _Adler32;
        public int AvailableBytesIn;
        public int AvailableBytesOut;
        public CompressionLevel CompressLevel;
        internal DeflateManager dstate;
        public byte[] InputBuffer;
        internal InflateManager istate;
        public string Message;
        public int NextIn;
        public int NextOut;
        public byte[] OutputBuffer;
        public CompressionStrategy Strategy;
        public long TotalBytesIn;
        public long TotalBytesOut;
        public int WindowBits;

        public ZlibCodec()
        {
            this.CompressLevel = CompressionLevel.Default;
            this.WindowBits = 15;
            this.Strategy = CompressionStrategy.Default;
        }

        public ZlibCodec(CompressionMode mode)
        {
            this.CompressLevel = CompressionLevel.Default;
            this.WindowBits = 15;
            this.Strategy = CompressionStrategy.Default;
            if (mode == CompressionMode.Compress)
            {
                if (this.InitializeDeflate() != 0)
                {
                    throw new ZlibException("Cannot initialize for deflate.");
                }
            }
            else
            {
                if (mode != CompressionMode.Decompress)
                {
                    throw new ZlibException("Invalid ZlibStreamFlavor.");
                }
                if (this.InitializeInflate() != 0)
                {
                    throw new ZlibException("Cannot initialize for inflate.");
                }
            }
        }

        private int _InternalInitializeDeflate(bool wantRfc1950Header)
        {
            if (this.istate != null)
            {
                throw new ZlibException("You may not call InitializeDeflate() after calling InitializeInflate().");
            }
            this.dstate = new DeflateManager();
            this.dstate.WantRfc1950HeaderBytes = wantRfc1950Header;
            return this.dstate.Initialize(this, this.CompressLevel, this.WindowBits, this.Strategy);
        }

        public int Deflate(FlushType flush)
        {
            if (this.dstate == null)
            {
                throw new ZlibException("No Deflate State!");
            }
            return this.dstate.Deflate(flush);
        }

        public int EndDeflate()
        {
            if (this.dstate == null)
            {
                throw new ZlibException("No Deflate State!");
            }
            this.dstate = null;
            return 0;
        }

        public int EndInflate()
        {
            if (this.istate == null)
            {
                throw new ZlibException("No Inflate State!");
            }
            int num = this.istate.End();
            this.istate = null;
            return num;
        }

        internal void flush_pending()
        {
            int pendingCount = this.dstate.pendingCount;
            if (pendingCount > this.AvailableBytesOut)
            {
                pendingCount = this.AvailableBytesOut;
            }
            if (pendingCount != 0)
            {
                if ((((this.dstate.pending.Length <= this.dstate.nextPending) || (this.OutputBuffer.Length <= this.NextOut)) || (this.dstate.pending.Length < (this.dstate.nextPending + pendingCount))) || (this.OutputBuffer.Length < (this.NextOut + pendingCount)))
                {
                    throw new ZlibException(string.Format("Invalid State. (pending.Length={0}, pendingCount={1})", this.dstate.pending.Length, this.dstate.pendingCount));
                }
                Array.Copy(this.dstate.pending, this.dstate.nextPending, this.OutputBuffer, this.NextOut, pendingCount);
                this.NextOut += pendingCount;
                this.dstate.nextPending += pendingCount;
                this.TotalBytesOut += pendingCount;
                this.AvailableBytesOut -= pendingCount;
                this.dstate.pendingCount -= pendingCount;
                if (this.dstate.pendingCount == 0)
                {
                    this.dstate.nextPending = 0;
                }
            }
        }

        public int Inflate(FlushType flush)
        {
            if (this.istate == null)
            {
                throw new ZlibException("No Inflate State!");
            }
            return this.istate.Inflate(flush);
        }

        public int InitializeDeflate()
        {
            return this._InternalInitializeDeflate(true);
        }

        public int InitializeDeflate(CompressionLevel level)
        {
            this.CompressLevel = level;
            return this._InternalInitializeDeflate(true);
        }

        public int InitializeDeflate(CompressionLevel level, bool wantRfc1950Header)
        {
            this.CompressLevel = level;
            return this._InternalInitializeDeflate(wantRfc1950Header);
        }

        public int InitializeDeflate(CompressionLevel level, int bits)
        {
            this.CompressLevel = level;
            this.WindowBits = bits;
            return this._InternalInitializeDeflate(true);
        }

        public int InitializeDeflate(CompressionLevel level, int bits, bool wantRfc1950Header)
        {
            this.CompressLevel = level;
            this.WindowBits = bits;
            return this._InternalInitializeDeflate(wantRfc1950Header);
        }

        public int InitializeInflate()
        {
            return this.InitializeInflate(this.WindowBits);
        }

        public int InitializeInflate(bool expectRfc1950Header)
        {
            return this.InitializeInflate(this.WindowBits, expectRfc1950Header);
        }

        public int InitializeInflate(int windowBits)
        {
            this.WindowBits = windowBits;
            return this.InitializeInflate(windowBits, true);
        }

        public int InitializeInflate(int windowBits, bool expectRfc1950Header)
        {
            this.WindowBits = windowBits;
            if (this.dstate != null)
            {
                throw new ZlibException("You may not call InitializeInflate() after calling InitializeDeflate().");
            }
            this.istate = new InflateManager(expectRfc1950Header);
            return this.istate.Initialize(this, windowBits);
        }

        internal int read_buf(byte[] buf, int start, int size)
        {
            int availableBytesIn = this.AvailableBytesIn;
            if (availableBytesIn > size)
            {
                availableBytesIn = size;
            }
            if (availableBytesIn == 0)
            {
                return 0;
            }
            this.AvailableBytesIn -= availableBytesIn;
            if (this.dstate.WantRfc1950HeaderBytes)
            {
                this._Adler32 = Adler.Adler32(this._Adler32, this.InputBuffer, this.NextIn, availableBytesIn);
            }
            Array.Copy(this.InputBuffer, this.NextIn, buf, start, availableBytesIn);
            this.NextIn += availableBytesIn;
            this.TotalBytesIn += availableBytesIn;
            return availableBytesIn;
        }

        public void ResetDeflate()
        {
            if (this.dstate == null)
            {
                throw new ZlibException("No Deflate State!");
            }
            this.dstate.Reset();
        }

        public int SetDeflateParams(CompressionLevel level, CompressionStrategy strategy)
        {
            if (this.dstate == null)
            {
                throw new ZlibException("No Deflate State!");
            }
            return this.dstate.SetParams(level, strategy);
        }

        public int SetDictionary(byte[] dictionary)
        {
            if (this.istate != null)
            {
                return this.istate.SetDictionary(dictionary);
            }
            if (this.dstate == null)
            {
                throw new ZlibException("No Inflate or Deflate state!");
            }
            return this.dstate.SetDictionary(dictionary);
        }

        public int SyncInflate()
        {
            if (this.istate == null)
            {
                throw new ZlibException("No Inflate State!");
            }
            return this.istate.Sync();
        }

        public int Adler32
        {
            get
            {
                return (int) this._Adler32;
            }
        }
    }
}

