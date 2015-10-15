namespace SharpCompress.Compressor.Deflate
{
    using System;

    internal sealed class InflateBlocks
    {
        internal ZlibCodec _codec;
        internal int[] bb = new int[1];
        internal int bitb;
        internal int bitk;
        internal int[] blens;
        internal static readonly int[] border = new int[] { 
            0x10, 0x11, 0x12, 0, 8, 7, 9, 6, 10, 5, 11, 4, 12, 3, 13, 2, 
            14, 1, 15
         };
        internal uint check;
        internal object checkfn;
        internal InflateCodes codes = new InflateCodes();
        internal int end;
        internal int[] hufts;
        internal int index;
        internal InfTree inftree = new InfTree();
        internal int last;
        internal int left;
        private const int MANY = 0x5a0;
        private InflateBlockMode mode;
        internal int readAt;
        internal int table;
        internal int[] tb = new int[1];
        internal byte[] window;
        internal int writeAt;

        internal InflateBlocks(ZlibCodec codec, object checkfn, int w)
        {
            this._codec = codec;
            this.hufts = new int[0x10e0];
            this.window = new byte[w];
            this.end = w;
            this.checkfn = checkfn;
            this.mode = InflateBlockMode.TYPE;
            this.Reset();
        }

        internal int Flush(int r)
        {
            for (int i = 0; i < 2; i++)
            {
                int availableBytesOut;
                if (i == 0)
                {
                    availableBytesOut = ((this.readAt <= this.writeAt) ? this.writeAt : this.end) - this.readAt;
                }
                else
                {
                    availableBytesOut = this.writeAt - this.readAt;
                }
                if (availableBytesOut == 0)
                {
                    if (r == -5)
                    {
                        r = 0;
                    }
                    return r;
                }
                if (availableBytesOut > this._codec.AvailableBytesOut)
                {
                    availableBytesOut = this._codec.AvailableBytesOut;
                }
                if ((availableBytesOut != 0) && (r == -5))
                {
                    r = 0;
                }
                this._codec.AvailableBytesOut -= availableBytesOut;
                this._codec.TotalBytesOut += availableBytesOut;
                if (this.checkfn != null)
                {
                    this._codec._Adler32 = this.check = Adler.Adler32(this.check, this.window, this.readAt, availableBytesOut);
                }
                Array.Copy(this.window, this.readAt, this._codec.OutputBuffer, this._codec.NextOut, availableBytesOut);
                this._codec.NextOut += availableBytesOut;
                this.readAt += availableBytesOut;
                if ((this.readAt == this.end) && (i == 0))
                {
                    this.readAt = 0;
                    if (this.writeAt == this.end)
                    {
                        this.writeAt = 0;
                    }
                }
                else
                {
                    i++;
                }
            }
            return r;
        }

        internal void Free()
        {
            this.Reset();
            this.window = null;
            this.hufts = null;
        }

        internal int Process(int r)
        {
            int[] numArray;
            int[] numArray2;
            bool flag;
            int nextIn = this._codec.NextIn;
            int availableBytesIn = this._codec.AvailableBytesIn;
            int bitb = this.bitb;
            int bitk = this.bitk;
            int writeAt = this.writeAt;
            int num7 = (writeAt < this.readAt) ? ((this.readAt - writeAt) - 1) : (this.end - writeAt);
            goto Label_1197;
        Label_0905:
            while (this.index < (4 + (this.table >> 10)))
            {
                while (bitk < 3)
                {
                    if (availableBytesIn != 0)
                    {
                        r = 0;
                    }
                    else
                    {
                        this.bitb = bitb;
                        this.bitk = bitk;
                        this._codec.AvailableBytesIn = availableBytesIn;
                        this._codec.TotalBytesIn += nextIn - this._codec.NextIn;
                        this._codec.NextIn = nextIn;
                        this.writeAt = writeAt;
                        return this.Flush(r);
                    }
                    availableBytesIn--;
                    bitb |= (this._codec.InputBuffer[nextIn++] & 0xff) << bitk;
                    bitk += 8;
                }
                this.blens[border[this.index++]] = bitb & 7;
                bitb = bitb >> 3;
                bitk -= 3;
            }
            while (this.index < 0x13)
            {
                this.blens[border[this.index++]] = 0;
            }
            this.bb[0] = 7;
            int index = this.inftree.inflate_trees_bits(this.blens, this.bb, this.tb, this.hufts, this._codec);
            if (index != 0)
            {
                r = index;
                if (r == -3)
                {
                    this.blens = null;
                    this.mode = InflateBlockMode.BAD;
                }
                this.bitb = bitb;
                this.bitk = bitk;
                this._codec.AvailableBytesIn = availableBytesIn;
                this._codec.TotalBytesIn += nextIn - this._codec.NextIn;
                this._codec.NextIn = nextIn;
                this.writeAt = writeAt;
                return this.Flush(r);
            }
            this.index = 0;
            this.mode = InflateBlockMode.DTREE;
        Label_0D8C:
            flag = true;
            index = this.table;
            if (this.index >= ((0x102 + (index & 0x1f)) + ((index >> 5) & 0x1f)))
            {
                this.tb[0] = -1;
                numArray = new int[] { 9 };
                numArray2 = new int[] { 6 };
                int[] tl = new int[1];
                int[] td = new int[1];
                index = this.table;
                index = this.inftree.inflate_trees_dynamic(0x101 + (index & 0x1f), 1 + ((index >> 5) & 0x1f), this.blens, numArray, numArray2, tl, td, this.hufts, this._codec);
                if (index != 0)
                {
                    if (index == -3)
                    {
                        this.blens = null;
                        this.mode = InflateBlockMode.BAD;
                    }
                    r = index;
                    this.bitb = bitb;
                    this.bitk = bitk;
                    this._codec.AvailableBytesIn = availableBytesIn;
                    this._codec.TotalBytesIn += nextIn - this._codec.NextIn;
                    this._codec.NextIn = nextIn;
                    this.writeAt = writeAt;
                    return this.Flush(r);
                }
                this.codes.Init(numArray[0], numArray2[0], this.hufts, tl[0], this.hufts, td[0]);
                this.mode = InflateBlockMode.CODES;
            }
            else
            {
                index = this.bb[0];
                while (bitk < index)
                {
                    if (availableBytesIn != 0)
                    {
                        r = 0;
                    }
                    else
                    {
                        this.bitb = bitb;
                        this.bitk = bitk;
                        this._codec.AvailableBytesIn = availableBytesIn;
                        this._codec.TotalBytesIn += nextIn - this._codec.NextIn;
                        this._codec.NextIn = nextIn;
                        this.writeAt = writeAt;
                        return this.Flush(r);
                    }
                    availableBytesIn--;
                    bitb |= (this._codec.InputBuffer[nextIn++] & 0xff) << bitk;
                    bitk += 8;
                }
                index = this.hufts[((this.tb[0] + (bitb & InternalInflateConstants.InflateMask[index])) * 3) + 1];
                int num10 = this.hufts[((this.tb[0] + (bitb & InternalInflateConstants.InflateMask[index])) * 3) + 2];
                if (num10 < 0x10)
                {
                    bitb = bitb >> index;
                    bitk -= index;
                    this.blens[this.index++] = num10;
                }
                else
                {
                    int num8 = (num10 == 0x12) ? 7 : (num10 - 14);
                    int num9 = (num10 == 0x12) ? 11 : 3;
                    while (bitk < (index + num8))
                    {
                        if (availableBytesIn != 0)
                        {
                            r = 0;
                        }
                        else
                        {
                            this.bitb = bitb;
                            this.bitk = bitk;
                            this._codec.AvailableBytesIn = availableBytesIn;
                            this._codec.TotalBytesIn += nextIn - this._codec.NextIn;
                            this._codec.NextIn = nextIn;
                            this.writeAt = writeAt;
                            return this.Flush(r);
                        }
                        availableBytesIn--;
                        bitb |= (this._codec.InputBuffer[nextIn++] & 0xff) << bitk;
                        bitk += 8;
                    }
                    bitb = bitb >> index;
                    bitk -= index;
                    num9 += bitb & InternalInflateConstants.InflateMask[num8];
                    bitb = bitb >> num8;
                    bitk -= num8;
                    num8 = this.index;
                    index = this.table;
                    if (((num8 + num9) > ((0x102 + (index & 0x1f)) + ((index >> 5) & 0x1f))) || ((num10 == 0x10) && (num8 < 1)))
                    {
                        this.blens = null;
                        this.mode = InflateBlockMode.BAD;
                        this._codec.Message = "invalid bit length repeat";
                        r = -3;
                        this.bitb = bitb;
                        this.bitk = bitk;
                        this._codec.AvailableBytesIn = availableBytesIn;
                        this._codec.TotalBytesIn += nextIn - this._codec.NextIn;
                        this._codec.NextIn = nextIn;
                        this.writeAt = writeAt;
                        return this.Flush(r);
                    }
                    num10 = (num10 == 0x10) ? this.blens[num8 - 1] : 0;
                    do
                    {
                        this.blens[num8++] = num10;
                    }
                    while (--num9 != 0);
                    this.index = num8;
                }
                goto Label_0D8C;
            }
        Label_0ECF:
            this.bitb = bitb;
            this.bitk = bitk;
            this._codec.AvailableBytesIn = availableBytesIn;
            this._codec.TotalBytesIn += nextIn - this._codec.NextIn;
            this._codec.NextIn = nextIn;
            this.writeAt = writeAt;
            r = this.codes.Process(this, r);
            if (r != 1)
            {
                return this.Flush(r);
            }
            r = 0;
            nextIn = this._codec.NextIn;
            availableBytesIn = this._codec.AvailableBytesIn;
            bitb = this.bitb;
            bitk = this.bitk;
            writeAt = this.writeAt;
            num7 = (writeAt < this.readAt) ? ((this.readAt - writeAt) - 1) : (this.end - writeAt);
            if (this.last == 0)
            {
                this.mode = InflateBlockMode.TYPE;
                goto Label_1197;
            }
            this.mode = InflateBlockMode.DRY;
        Label_0FC3:
            this.writeAt = writeAt;
            r = this.Flush(r);
            writeAt = this.writeAt;
            num7 = (writeAt < this.readAt) ? ((this.readAt - writeAt) - 1) : (this.end - writeAt);
            if (this.readAt != this.writeAt)
            {
                this.bitb = bitb;
                this.bitk = bitk;
                this._codec.AvailableBytesIn = availableBytesIn;
                this._codec.TotalBytesIn += nextIn - this._codec.NextIn;
                this._codec.NextIn = nextIn;
                this.writeAt = writeAt;
                return this.Flush(r);
            }
            this.mode = InflateBlockMode.DONE;
        Label_107A:
            r = 1;
            this.bitb = bitb;
            this.bitk = bitk;
            this._codec.AvailableBytesIn = availableBytesIn;
            this._codec.TotalBytesIn += nextIn - this._codec.NextIn;
            this._codec.NextIn = nextIn;
            this.writeAt = writeAt;
            return this.Flush(r);
        Label_1197:
            flag = true;
            switch (this.mode)
            {
                case InflateBlockMode.TYPE:
                    while (bitk < 3)
                    {
                        if (availableBytesIn != 0)
                        {
                            r = 0;
                        }
                        else
                        {
                            this.bitb = bitb;
                            this.bitk = bitk;
                            this._codec.AvailableBytesIn = availableBytesIn;
                            this._codec.TotalBytesIn += nextIn - this._codec.NextIn;
                            this._codec.NextIn = nextIn;
                            this.writeAt = writeAt;
                            return this.Flush(r);
                        }
                        availableBytesIn--;
                        bitb |= (this._codec.InputBuffer[nextIn++] & 0xff) << bitk;
                        bitk += 8;
                    }
                    index = bitb & 7;
                    this.last = index & 1;
                    switch (((uint) (index >> 1)))
                    {
                        case 0:
                            bitb = bitb >> 3;
                            bitk -= 3;
                            index = bitk & 7;
                            bitb = bitb >> index;
                            bitk -= index;
                            this.mode = InflateBlockMode.LENS;
                            break;

                        case 1:
                        {
                            numArray = new int[1];
                            numArray2 = new int[1];
                            int[][] numArray3 = new int[1][];
                            int[][] numArray4 = new int[1][];
                            InfTree.inflate_trees_fixed(numArray, numArray2, numArray3, numArray4, this._codec);
                            this.codes.Init(numArray[0], numArray2[0], numArray3[0], 0, numArray4[0], 0);
                            bitb = bitb >> 3;
                            bitk -= 3;
                            this.mode = InflateBlockMode.CODES;
                            break;
                        }
                        case 2:
                            bitb = bitb >> 3;
                            bitk -= 3;
                            this.mode = InflateBlockMode.TABLE;
                            break;

                        case 3:
                            bitb = bitb >> 3;
                            bitk -= 3;
                            this.mode = InflateBlockMode.BAD;
                            this._codec.Message = "invalid block type";
                            r = -3;
                            this.bitb = bitb;
                            this.bitk = bitk;
                            this._codec.AvailableBytesIn = availableBytesIn;
                            this._codec.TotalBytesIn += nextIn - this._codec.NextIn;
                            this._codec.NextIn = nextIn;
                            this.writeAt = writeAt;
                            return this.Flush(r);
                    }
                    goto Label_1197;

                case InflateBlockMode.LENS:
                    while (bitk < 0x20)
                    {
                        if (availableBytesIn != 0)
                        {
                            r = 0;
                        }
                        else
                        {
                            this.bitb = bitb;
                            this.bitk = bitk;
                            this._codec.AvailableBytesIn = availableBytesIn;
                            this._codec.TotalBytesIn += nextIn - this._codec.NextIn;
                            this._codec.NextIn = nextIn;
                            this.writeAt = writeAt;
                            return this.Flush(r);
                        }
                        availableBytesIn--;
                        bitb |= (this._codec.InputBuffer[nextIn++] & 0xff) << bitk;
                        bitk += 8;
                    }
                    if (((~bitb >> 0x10) & 0xffff) != (bitb & 0xffff))
                    {
                        this.mode = InflateBlockMode.BAD;
                        this._codec.Message = "invalid stored block lengths";
                        r = -3;
                        this.bitb = bitb;
                        this.bitk = bitk;
                        this._codec.AvailableBytesIn = availableBytesIn;
                        this._codec.TotalBytesIn += nextIn - this._codec.NextIn;
                        this._codec.NextIn = nextIn;
                        this.writeAt = writeAt;
                        return this.Flush(r);
                    }
                    this.left = bitb & 0xffff;
                    bitb = bitk = 0;
                    this.mode = (this.left != 0) ? InflateBlockMode.STORED : ((this.last != 0) ? InflateBlockMode.DRY : InflateBlockMode.TYPE);
                    goto Label_1197;

                case InflateBlockMode.STORED:
                    if (availableBytesIn != 0)
                    {
                        if (num7 == 0)
                        {
                            if ((writeAt == this.end) && (this.readAt != 0))
                            {
                                writeAt = 0;
                                num7 = (writeAt < this.readAt) ? ((this.readAt - writeAt) - 1) : (this.end - writeAt);
                            }
                            if (num7 == 0)
                            {
                                this.writeAt = writeAt;
                                r = this.Flush(r);
                                writeAt = this.writeAt;
                                num7 = (writeAt < this.readAt) ? ((this.readAt - writeAt) - 1) : (this.end - writeAt);
                                if ((writeAt == this.end) && (this.readAt != 0))
                                {
                                    writeAt = 0;
                                    num7 = (writeAt < this.readAt) ? ((this.readAt - writeAt) - 1) : (this.end - writeAt);
                                }
                                if (num7 == 0)
                                {
                                    this.bitb = bitb;
                                    this.bitk = bitk;
                                    this._codec.AvailableBytesIn = availableBytesIn;
                                    this._codec.TotalBytesIn += nextIn - this._codec.NextIn;
                                    this._codec.NextIn = nextIn;
                                    this.writeAt = writeAt;
                                    return this.Flush(r);
                                }
                            }
                        }
                        r = 0;
                        index = this.left;
                        if (index > availableBytesIn)
                        {
                            index = availableBytesIn;
                        }
                        if (index > num7)
                        {
                            index = num7;
                        }
                        Array.Copy(this._codec.InputBuffer, nextIn, this.window, writeAt, index);
                        nextIn += index;
                        availableBytesIn -= index;
                        writeAt += index;
                        num7 -= index;
                        this.left -= index;
                        if (this.left == 0)
                        {
                            this.mode = (this.last != 0) ? InflateBlockMode.DRY : InflateBlockMode.TYPE;
                        }
                        goto Label_1197;
                    }
                    this.bitb = bitb;
                    this.bitk = bitk;
                    this._codec.AvailableBytesIn = availableBytesIn;
                    this._codec.TotalBytesIn += nextIn - this._codec.NextIn;
                    this._codec.NextIn = nextIn;
                    this.writeAt = writeAt;
                    return this.Flush(r);

                case InflateBlockMode.TABLE:
                    while (bitk < 14)
                    {
                        if (availableBytesIn != 0)
                        {
                            r = 0;
                        }
                        else
                        {
                            this.bitb = bitb;
                            this.bitk = bitk;
                            this._codec.AvailableBytesIn = availableBytesIn;
                            this._codec.TotalBytesIn += nextIn - this._codec.NextIn;
                            this._codec.NextIn = nextIn;
                            this.writeAt = writeAt;
                            return this.Flush(r);
                        }
                        availableBytesIn--;
                        bitb |= (this._codec.InputBuffer[nextIn++] & 0xff) << bitk;
                        bitk += 8;
                    }
                    this.table = index = bitb & 0x3fff;
                    if (((index & 0x1f) > 0x1d) || (((index >> 5) & 0x1f) > 0x1d))
                    {
                        this.mode = InflateBlockMode.BAD;
                        this._codec.Message = "too many length or distance symbols";
                        r = -3;
                        this.bitb = bitb;
                        this.bitk = bitk;
                        this._codec.AvailableBytesIn = availableBytesIn;
                        this._codec.TotalBytesIn += nextIn - this._codec.NextIn;
                        this._codec.NextIn = nextIn;
                        this.writeAt = writeAt;
                        return this.Flush(r);
                    }
                    index = (0x102 + (index & 0x1f)) + ((index >> 5) & 0x1f);
                    if ((this.blens == null) || (this.blens.Length < index))
                    {
                        this.blens = new int[index];
                    }
                    else
                    {
                        Array.Clear(this.blens, 0, index);
                    }
                    bitb = bitb >> 14;
                    bitk -= 14;
                    this.index = 0;
                    this.mode = InflateBlockMode.BTREE;
                    goto Label_0905;

                case InflateBlockMode.BTREE:
                    goto Label_0905;

                case InflateBlockMode.DTREE:
                    goto Label_0D8C;

                case InflateBlockMode.CODES:
                    goto Label_0ECF;

                case InflateBlockMode.DRY:
                    goto Label_0FC3;

                case InflateBlockMode.DONE:
                    goto Label_107A;

                case InflateBlockMode.BAD:
                    r = -3;
                    this.bitb = bitb;
                    this.bitk = bitk;
                    this._codec.AvailableBytesIn = availableBytesIn;
                    this._codec.TotalBytesIn += nextIn - this._codec.NextIn;
                    this._codec.NextIn = nextIn;
                    this.writeAt = writeAt;
                    return this.Flush(r);
            }
            r = -2;
            this.bitb = bitb;
            this.bitk = bitk;
            this._codec.AvailableBytesIn = availableBytesIn;
            this._codec.TotalBytesIn += nextIn - this._codec.NextIn;
            this._codec.NextIn = nextIn;
            this.writeAt = writeAt;
            return this.Flush(r);
        }

        internal uint Reset()
        {
            uint check = this.check;
            this.mode = InflateBlockMode.TYPE;
            this.bitk = 0;
            this.bitb = 0;
            this.readAt = this.writeAt = 0;
            if (this.checkfn != null)
            {
                this._codec._Adler32 = this.check = Adler.Adler32(0, null, 0, 0);
            }
            return check;
        }

        internal void SetDictionary(byte[] d, int start, int n)
        {
            Array.Copy(d, start, this.window, 0, n);
            this.readAt = this.writeAt = n;
        }

        internal int SyncPoint()
        {
            return ((this.mode == InflateBlockMode.LENS) ? 1 : 0);
        }

        private enum InflateBlockMode
        {
            TYPE,
            LENS,
            STORED,
            TABLE,
            BTREE,
            DTREE,
            CODES,
            DRY,
            DONE,
            BAD
        }
    }
}

