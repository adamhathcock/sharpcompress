namespace SharpCompress.Compressor.Deflate
{
    using System;

    internal sealed class InflateManager
    {
        internal ZlibCodec _codec;
        private bool _handleRfc1950HeaderBytes;
        internal InflateBlocks blocks;
        internal uint computedCheck;
        internal uint expectedCheck;
        private static readonly byte[] mark;
        internal int marker;
        internal int method;
        private InflateManagerMode mode;
        private const int PRESET_DICT = 0x20;
        internal int wbits;
        private const int Z_DEFLATED = 8;

        static InflateManager()
        {
            byte[] buffer = new byte[4];
            buffer[2] = 0xff;
            buffer[3] = 0xff;
            mark = buffer;
        }

        public InflateManager()
        {
            this._handleRfc1950HeaderBytes = true;
        }

        public InflateManager(bool expectRfc1950HeaderBytes)
        {
            this._handleRfc1950HeaderBytes = true;
            this._handleRfc1950HeaderBytes = expectRfc1950HeaderBytes;
        }

        internal int End()
        {
            if (this.blocks != null)
            {
                this.blocks.Free();
            }
            this.blocks = null;
            return 0;
        }

        internal int Inflate(FlushType flush)
        {
            bool flag;
            if (this._codec.InputBuffer == null)
            {
                throw new ZlibException("InputBuffer is null. ");
            }
            int num2 = 0;
            int r = -5;
        Label_07D5:
            flag = true;
            switch (this.mode)
            {
                case InflateManagerMode.METHOD:
                    if (this._codec.AvailableBytesIn != 0)
                    {
                        r = num2;
                        this._codec.AvailableBytesIn--;
                        this._codec.TotalBytesIn += 1L;
                        if (((this.method = this._codec.InputBuffer[this._codec.NextIn++]) & 15) != 8)
                        {
                            this.mode = InflateManagerMode.BAD;
                            this._codec.Message = string.Format("unknown compression method (0x{0:X2})", this.method);
                            this.marker = 5;
                        }
                        else if (((this.method >> 4) + 8) > this.wbits)
                        {
                            this.mode = InflateManagerMode.BAD;
                            this._codec.Message = string.Format("invalid window size ({0})", (this.method >> 4) + 8);
                            this.marker = 5;
                        }
                        else
                        {
                            this.mode = InflateManagerMode.FLAG;
                        }
                        goto Label_07D5;
                    }
                    return r;

                case InflateManagerMode.FLAG:
                    if (this._codec.AvailableBytesIn != 0)
                    {
                        r = num2;
                        this._codec.AvailableBytesIn--;
                        this._codec.TotalBytesIn += 1L;
                        int num = this._codec.InputBuffer[this._codec.NextIn++] & 0xff;
                        if ((((this.method << 8) + num) % 0x1f) != 0)
                        {
                            this.mode = InflateManagerMode.BAD;
                            this._codec.Message = "incorrect header check";
                            this.marker = 5;
                        }
                        else
                        {
                            this.mode = ((num & 0x20) == 0) ? InflateManagerMode.BLOCKS : InflateManagerMode.DICT4;
                        }
                        goto Label_07D5;
                    }
                    return r;

                case InflateManagerMode.DICT4:
                    if (this._codec.AvailableBytesIn != 0)
                    {
                        r = num2;
                        this._codec.AvailableBytesIn--;
                        this._codec.TotalBytesIn += 1L;
                        this.expectedCheck = (uint) ((this._codec.InputBuffer[this._codec.NextIn++] << 0x18) & 0xff000000L);
                        this.mode = InflateManagerMode.DICT3;
                        goto Label_07D5;
                    }
                    return r;

                case InflateManagerMode.DICT3:
                    if (this._codec.AvailableBytesIn != 0)
                    {
                        r = num2;
                        this._codec.AvailableBytesIn--;
                        this._codec.TotalBytesIn += 1L;
                        this.expectedCheck += (uint) ((this._codec.InputBuffer[this._codec.NextIn++] << 0x10) & 0xff0000);
                        this.mode = InflateManagerMode.DICT2;
                        goto Label_07D5;
                    }
                    return r;

                case InflateManagerMode.DICT2:
                    if (this._codec.AvailableBytesIn != 0)
                    {
                        r = num2;
                        this._codec.AvailableBytesIn--;
                        this._codec.TotalBytesIn += 1L;
                        this.expectedCheck += (uint) ((this._codec.InputBuffer[this._codec.NextIn++] << 8) & 0xff00);
                        this.mode = InflateManagerMode.DICT1;
                        goto Label_07D5;
                    }
                    return r;

                case InflateManagerMode.DICT1:
                    if (this._codec.AvailableBytesIn != 0)
                    {
                        r = num2;
                        this._codec.AvailableBytesIn--;
                        this._codec.TotalBytesIn += 1L;
                        this.expectedCheck += (uint) (this._codec.InputBuffer[this._codec.NextIn++] & 0xff);
                        this._codec._Adler32 = this.expectedCheck;
                        this.mode = InflateManagerMode.DICT0;
                        return 2;
                    }
                    return r;

                case InflateManagerMode.DICT0:
                    this.mode = InflateManagerMode.BAD;
                    this._codec.Message = "need dictionary";
                    this.marker = 0;
                    return -2;

                case InflateManagerMode.BLOCKS:
                    r = this.blocks.Process(r);
                    if (r != -3)
                    {
                        if (r == 0)
                        {
                            r = num2;
                        }
                        if (r != 1)
                        {
                            return r;
                        }
                        r = num2;
                        this.computedCheck = this.blocks.Reset();
                        if (!this.HandleRfc1950HeaderBytes)
                        {
                            this.mode = InflateManagerMode.DONE;
                            return 1;
                        }
                        this.mode = InflateManagerMode.CHECK4;
                    }
                    else
                    {
                        this.mode = InflateManagerMode.BAD;
                        this.marker = 0;
                    }
                    goto Label_07D5;

                case InflateManagerMode.CHECK4:
                    if (this._codec.AvailableBytesIn != 0)
                    {
                        r = num2;
                        this._codec.AvailableBytesIn--;
                        this._codec.TotalBytesIn += 1L;
                        this.expectedCheck = (uint) ((this._codec.InputBuffer[this._codec.NextIn++] << 0x18) & 0xff000000L);
                        this.mode = InflateManagerMode.CHECK3;
                        goto Label_07D5;
                    }
                    return r;

                case InflateManagerMode.CHECK3:
                    if (this._codec.AvailableBytesIn != 0)
                    {
                        r = num2;
                        this._codec.AvailableBytesIn--;
                        this._codec.TotalBytesIn += 1L;
                        this.expectedCheck += (uint) ((this._codec.InputBuffer[this._codec.NextIn++] << 0x10) & 0xff0000);
                        this.mode = InflateManagerMode.CHECK2;
                        goto Label_07D5;
                    }
                    return r;

                case InflateManagerMode.CHECK2:
                    if (this._codec.AvailableBytesIn != 0)
                    {
                        r = num2;
                        this._codec.AvailableBytesIn--;
                        this._codec.TotalBytesIn += 1L;
                        this.expectedCheck += (uint) ((this._codec.InputBuffer[this._codec.NextIn++] << 8) & 0xff00);
                        this.mode = InflateManagerMode.CHECK1;
                        goto Label_07D5;
                    }
                    return r;

                case InflateManagerMode.CHECK1:
                    if (this._codec.AvailableBytesIn != 0)
                    {
                        r = num2;
                        this._codec.AvailableBytesIn--;
                        this._codec.TotalBytesIn += 1L;
                        this.expectedCheck += (uint) (this._codec.InputBuffer[this._codec.NextIn++] & 0xff);
                        if (this.computedCheck != this.expectedCheck)
                        {
                            this.mode = InflateManagerMode.BAD;
                            this._codec.Message = "incorrect data check";
                            this.marker = 5;
                        }
                        else
                        {
                            this.mode = InflateManagerMode.DONE;
                            return 1;
                        }
                        goto Label_07D5;
                    }
                    return r;

                case InflateManagerMode.DONE:
                    return 1;

                case InflateManagerMode.BAD:
                    throw new ZlibException(string.Format("Bad state ({0})", this._codec.Message));
            }
            throw new ZlibException("Stream error.");
        }

        internal int Initialize(ZlibCodec codec, int w)
        {
            this._codec = codec;
            this._codec.Message = null;
            this.blocks = null;
            if ((w < 8) || (w > 15))
            {
                this.End();
                throw new ZlibException("Bad window size.");
            }
            this.wbits = w;
            this.blocks = new InflateBlocks(codec, this.HandleRfc1950HeaderBytes ? this : null, ((int) 1) << w);
            this.Reset();
            return 0;
        }

        internal int Reset()
        {
            this._codec.TotalBytesIn = this._codec.TotalBytesOut = 0L;
            this._codec.Message = null;
            this.mode = this.HandleRfc1950HeaderBytes ? InflateManagerMode.METHOD : InflateManagerMode.BLOCKS;
            this.blocks.Reset();
            return 0;
        }

        internal int SetDictionary(byte[] dictionary)
        {
            int start = 0;
            int length = dictionary.Length;
            if (this.mode != InflateManagerMode.DICT0)
            {
                throw new ZlibException("Stream error.");
            }
            if (Adler.Adler32(1, dictionary, 0, dictionary.Length) != this._codec._Adler32)
            {
                return -3;
            }
            this._codec._Adler32 = Adler.Adler32(0, null, 0, 0);
            if (length >= (((int) 1) << this.wbits))
            {
                length = (((int) 1) << this.wbits) - 1;
                start = dictionary.Length - length;
            }
            this.blocks.SetDictionary(dictionary, start, length);
            this.mode = InflateManagerMode.BLOCKS;
            return 0;
        }

        internal int Sync()
        {
            if (this.mode != InflateManagerMode.BAD)
            {
                this.mode = InflateManagerMode.BAD;
                this.marker = 0;
            }
            int availableBytesIn = this._codec.AvailableBytesIn;
            if (availableBytesIn == 0)
            {
                return -5;
            }
            int nextIn = this._codec.NextIn;
            int marker = this.marker;
            while ((availableBytesIn != 0) && (marker < 4))
            {
                if (this._codec.InputBuffer[nextIn] == mark[marker])
                {
                    marker++;
                }
                else if (this._codec.InputBuffer[nextIn] != 0)
                {
                    marker = 0;
                }
                else
                {
                    marker = 4 - marker;
                }
                nextIn++;
                availableBytesIn--;
            }
            this._codec.TotalBytesIn += nextIn - this._codec.NextIn;
            this._codec.NextIn = nextIn;
            this._codec.AvailableBytesIn = availableBytesIn;
            this.marker = marker;
            if (marker != 4)
            {
                return -3;
            }
            long totalBytesIn = this._codec.TotalBytesIn;
            long totalBytesOut = this._codec.TotalBytesOut;
            this.Reset();
            this._codec.TotalBytesIn = totalBytesIn;
            this._codec.TotalBytesOut = totalBytesOut;
            this.mode = InflateManagerMode.BLOCKS;
            return 0;
        }

        internal int SyncPoint(ZlibCodec z)
        {
            return this.blocks.SyncPoint();
        }

        internal bool HandleRfc1950HeaderBytes
        {
            get
            {
                return this._handleRfc1950HeaderBytes;
            }
            set
            {
                this._handleRfc1950HeaderBytes = value;
            }
        }

        private enum InflateManagerMode
        {
            METHOD,
            FLAG,
            DICT4,
            DICT3,
            DICT2,
            DICT1,
            DICT0,
            BLOCKS,
            CHECK4,
            CHECK3,
            CHECK2,
            CHECK1,
            DONE,
            BAD
        }
    }
}

