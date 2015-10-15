namespace SharpCompress.Compressor.Deflate
{
    using System;

    internal sealed class InflateCodes
    {
        private const int BADCODE = 9;
        internal int bitsToGet;
        private const int COPY = 5;
        internal byte dbits;
        internal int dist;
        private const int DIST = 3;
        private const int DISTEXT = 4;
        internal int[] dtree;
        internal int dtree_index;
        private const int END = 8;
        internal byte lbits;
        internal int len;
        private const int LEN = 1;
        private const int LENEXT = 2;
        internal int lit;
        private const int LIT = 6;
        internal int[] ltree;
        internal int ltree_index;
        internal int mode;
        internal int need;
        private const int START = 0;
        internal int[] tree;
        internal int tree_index;
        private const int WASH = 7;

        internal int InflateFast(int bl, int bd, int[] tl, int tl_index, int[] td, int td_index, InflateBlocks s, ZlibCodec z)
        {
            int num12;
            int nextIn = z.NextIn;
            int availableBytesIn = z.AvailableBytesIn;
            int bitb = s.bitb;
            int bitk = s.bitk;
            int writeAt = s.writeAt;
            int num9 = (writeAt < s.readAt) ? ((s.readAt - writeAt) - 1) : (s.end - writeAt);
            int num10 = InternalInflateConstants.InflateMask[bl];
            int num11 = InternalInflateConstants.InflateMask[bd];
            do
            {
                while (bitk < 20)
                {
                    availableBytesIn--;
                    bitb |= (z.InputBuffer[nextIn++] & 0xff) << bitk;
                    bitk += 8;
                }
                int num = bitb & num10;
                int[] numArray = tl;
                int num2 = tl_index;
                int index = (num2 + num) * 3;
                int num3 = numArray[index];
                if (num3 == 0)
                {
                    bitb = bitb >> numArray[index + 1];
                    bitk -= numArray[index + 1];
                    s.window[writeAt++] = (byte) numArray[index + 2];
                    num9--;
                }
                else
                {
                    while (true)
                    {
                        bool flag;
                        bitb = bitb >> numArray[index + 1];
                        bitk -= numArray[index + 1];
                        if ((num3 & 0x10) != 0)
                        {
                            num3 &= 15;
                            num12 = numArray[index + 2] + (bitb & InternalInflateConstants.InflateMask[num3]);
                            bitb = bitb >> num3;
                            bitk -= num3;
                            while (bitk < 15)
                            {
                                availableBytesIn--;
                                bitb |= (z.InputBuffer[nextIn++] & 0xff) << bitk;
                                bitk += 8;
                            }
                            num = bitb & num11;
                            numArray = td;
                            num2 = td_index;
                            index = (num2 + num) * 3;
                            num3 = numArray[index];
                            while (true)
                            {
                                bitb = bitb >> numArray[index + 1];
                                bitk -= numArray[index + 1];
                                if ((num3 & 0x10) != 0)
                                {
                                    int num14;
                                    num3 &= 15;
                                    while (bitk < num3)
                                    {
                                        availableBytesIn--;
                                        bitb |= (z.InputBuffer[nextIn++] & 0xff) << bitk;
                                        bitk += 8;
                                    }
                                    int num13 = numArray[index + 2] + (bitb & InternalInflateConstants.InflateMask[num3]);
                                    bitb = bitb >> num3;
                                    bitk -= num3;
                                    num9 -= num12;
                                    if (writeAt >= num13)
                                    {
                                        num14 = writeAt - num13;
                                        if (((writeAt - num14) > 0) && (2 > (writeAt - num14)))
                                        {
                                            s.window[writeAt++] = s.window[num14++];
                                            s.window[writeAt++] = s.window[num14++];
                                            num12 -= 2;
                                        }
                                        else
                                        {
                                            Array.Copy(s.window, num14, s.window, writeAt, 2);
                                            writeAt += 2;
                                            num14 += 2;
                                            num12 -= 2;
                                        }
                                    }
                                    else
                                    {
                                        num14 = writeAt - num13;
                                        do
                                        {
                                            num14 += s.end;
                                        }
                                        while (num14 < 0);
                                        num3 = s.end - num14;
                                        if (num12 > num3)
                                        {
                                            num12 -= num3;
                                            if (((writeAt - num14) > 0) && (num3 > (writeAt - num14)))
                                            {
                                                do
                                                {
                                                    s.window[writeAt++] = s.window[num14++];
                                                }
                                                while (--num3 != 0);
                                            }
                                            else
                                            {
                                                Array.Copy(s.window, num14, s.window, writeAt, num3);
                                                writeAt += num3;
                                                num14 += num3;
                                                num3 = 0;
                                            }
                                            num14 = 0;
                                        }
                                    }
                                    if (((writeAt - num14) > 0) && (num12 > (writeAt - num14)))
                                    {
                                        do
                                        {
                                            s.window[writeAt++] = s.window[num14++];
                                        }
                                        while (--num12 != 0);
                                    }
                                    else
                                    {
                                        Array.Copy(s.window, num14, s.window, writeAt, num12);
                                        writeAt += num12;
                                        num14 += num12;
                                        num12 = 0;
                                    }
                                    break;
                                }
                                if ((num3 & 0x40) == 0)
                                {
                                    num += numArray[index + 2];
                                    num += bitb & InternalInflateConstants.InflateMask[num3];
                                    index = (num2 + num) * 3;
                                    num3 = numArray[index];
                                }
                                else
                                {
                                    z.Message = "invalid distance code";
                                    num12 = z.AvailableBytesIn - availableBytesIn;
                                    num12 = ((bitk >> 3) < num12) ? (bitk >> 3) : num12;
                                    availableBytesIn += num12;
                                    nextIn -= num12;
                                    bitk -= num12 << 3;
                                    s.bitb = bitb;
                                    s.bitk = bitk;
                                    z.AvailableBytesIn = availableBytesIn;
                                    z.TotalBytesIn += nextIn - z.NextIn;
                                    z.NextIn = nextIn;
                                    s.writeAt = writeAt;
                                    return -3;
                                }
                                flag = true;
                            }
                        }
                        if ((num3 & 0x40) == 0)
                        {
                            num += numArray[index + 2];
                            num += bitb & InternalInflateConstants.InflateMask[num3];
                            index = (num2 + num) * 3;
                            num3 = numArray[index];
                            if (num3 == 0)
                            {
                                bitb = bitb >> numArray[index + 1];
                                bitk -= numArray[index + 1];
                                s.window[writeAt++] = (byte) numArray[index + 2];
                                num9--;
                                break;
                            }
                        }
                        else
                        {
                            if ((num3 & 0x20) != 0)
                            {
                                num12 = z.AvailableBytesIn - availableBytesIn;
                                num12 = ((bitk >> 3) < num12) ? (bitk >> 3) : num12;
                                availableBytesIn += num12;
                                nextIn -= num12;
                                bitk -= num12 << 3;
                                s.bitb = bitb;
                                s.bitk = bitk;
                                z.AvailableBytesIn = availableBytesIn;
                                z.TotalBytesIn += nextIn - z.NextIn;
                                z.NextIn = nextIn;
                                s.writeAt = writeAt;
                                return 1;
                            }
                            z.Message = "invalid literal/length code";
                            num12 = z.AvailableBytesIn - availableBytesIn;
                            num12 = ((bitk >> 3) < num12) ? (bitk >> 3) : num12;
                            availableBytesIn += num12;
                            nextIn -= num12;
                            bitk -= num12 << 3;
                            s.bitb = bitb;
                            s.bitk = bitk;
                            z.AvailableBytesIn = availableBytesIn;
                            z.TotalBytesIn += nextIn - z.NextIn;
                            z.NextIn = nextIn;
                            s.writeAt = writeAt;
                            return -3;
                        }
                        flag = true;
                    }
                }
            }
            while ((num9 >= 0x102) && (availableBytesIn >= 10));
            num12 = z.AvailableBytesIn - availableBytesIn;
            num12 = ((bitk >> 3) < num12) ? (bitk >> 3) : num12;
            availableBytesIn += num12;
            nextIn -= num12;
            bitk -= num12 << 3;
            s.bitb = bitb;
            s.bitk = bitk;
            z.AvailableBytesIn = availableBytesIn;
            z.TotalBytesIn += nextIn - z.NextIn;
            z.NextIn = nextIn;
            s.writeAt = writeAt;
            return 0;
        }

        internal void Init(int bl, int bd, int[] tl, int tl_index, int[] td, int td_index)
        {
            this.mode = 0;
            this.lbits = (byte) bl;
            this.dbits = (byte) bd;
            this.ltree = tl;
            this.ltree_index = tl_index;
            this.dtree = td;
            this.dtree_index = td_index;
            this.tree = null;
        }

        internal int Process(InflateBlocks blocks, int r)
        {
            int bitsToGet;
            int num10;
            bool flag;
            int bitb = 0;
            int bitk = 0;
            int nextIn = 0;
            ZlibCodec z = blocks._codec;
            nextIn = z.NextIn;
            int availableBytesIn = z.AvailableBytesIn;
            bitb = blocks.bitb;
            bitk = blocks.bitk;
            int writeAt = blocks.writeAt;
            int num9 = (writeAt < blocks.readAt) ? ((blocks.readAt - writeAt) - 1) : (blocks.end - writeAt);
            goto Label_0C48;
        Label_019A:
            this.need = this.lbits;
            this.tree = this.ltree;
            this.tree_index = this.ltree_index;
            this.mode = 1;
        Label_01C7:
            bitsToGet = this.need;
            while (bitk < bitsToGet)
            {
                if (availableBytesIn != 0)
                {
                    r = 0;
                }
                else
                {
                    blocks.bitb = bitb;
                    blocks.bitk = bitk;
                    z.AvailableBytesIn = availableBytesIn;
                    z.TotalBytesIn += nextIn - z.NextIn;
                    z.NextIn = nextIn;
                    blocks.writeAt = writeAt;
                    return blocks.Flush(r);
                }
                availableBytesIn--;
                bitb |= (z.InputBuffer[nextIn++] & 0xff) << bitk;
                bitk += 8;
            }
            int index = (this.tree_index + (bitb & InternalInflateConstants.InflateMask[bitsToGet])) * 3;
            bitb = bitb >> this.tree[index + 1];
            bitk -= this.tree[index + 1];
            int num3 = this.tree[index];
            if (num3 == 0)
            {
                this.lit = this.tree[index + 2];
                this.mode = 6;
                goto Label_0C48;
            }
            if ((num3 & 0x10) != 0)
            {
                this.bitsToGet = num3 & 15;
                this.len = this.tree[index + 2];
                this.mode = 2;
                goto Label_0C48;
            }
            if ((num3 & 0x40) == 0)
            {
                this.need = num3;
                this.tree_index = (index / 3) + this.tree[index + 2];
                goto Label_0C48;
            }
            if ((num3 & 0x20) != 0)
            {
                this.mode = 7;
                goto Label_0C48;
            }
            this.mode = 9;
            z.Message = "invalid literal/length code";
            r = -3;
            blocks.bitb = bitb;
            blocks.bitk = bitk;
            z.AvailableBytesIn = availableBytesIn;
            z.TotalBytesIn += nextIn - z.NextIn;
            z.NextIn = nextIn;
            blocks.writeAt = writeAt;
            return blocks.Flush(r);
        Label_04B1:
            bitsToGet = this.need;
            while (bitk < bitsToGet)
            {
                if (availableBytesIn != 0)
                {
                    r = 0;
                }
                else
                {
                    blocks.bitb = bitb;
                    blocks.bitk = bitk;
                    z.AvailableBytesIn = availableBytesIn;
                    z.TotalBytesIn += nextIn - z.NextIn;
                    z.NextIn = nextIn;
                    blocks.writeAt = writeAt;
                    return blocks.Flush(r);
                }
                availableBytesIn--;
                bitb |= (z.InputBuffer[nextIn++] & 0xff) << bitk;
                bitk += 8;
            }
            index = (this.tree_index + (bitb & InternalInflateConstants.InflateMask[bitsToGet])) * 3;
            bitb = bitb >> this.tree[index + 1];
            bitk -= this.tree[index + 1];
            num3 = this.tree[index];
            if ((num3 & 0x10) != 0)
            {
                this.bitsToGet = num3 & 15;
                this.dist = this.tree[index + 2];
                this.mode = 4;
                goto Label_0C48;
            }
            if ((num3 & 0x40) == 0)
            {
                this.need = num3;
                this.tree_index = (index / 3) + this.tree[index + 2];
                goto Label_0C48;
            }
            this.mode = 9;
            z.Message = "invalid distance code";
            r = -3;
            blocks.bitb = bitb;
            blocks.bitk = bitk;
            z.AvailableBytesIn = availableBytesIn;
            z.TotalBytesIn += nextIn - z.NextIn;
            z.NextIn = nextIn;
            blocks.writeAt = writeAt;
            return blocks.Flush(r);
        Label_0733:
            num10 = writeAt - this.dist;
            while (num10 < 0)
            {
                num10 += blocks.end;
            }
            while (this.len != 0)
            {
                if (num9 == 0)
                {
                    if ((writeAt == blocks.end) && (blocks.readAt != 0))
                    {
                        writeAt = 0;
                        num9 = (writeAt < blocks.readAt) ? ((blocks.readAt - writeAt) - 1) : (blocks.end - writeAt);
                    }
                    if (num9 == 0)
                    {
                        blocks.writeAt = writeAt;
                        r = blocks.Flush(r);
                        writeAt = blocks.writeAt;
                        num9 = (writeAt < blocks.readAt) ? ((blocks.readAt - writeAt) - 1) : (blocks.end - writeAt);
                        if ((writeAt == blocks.end) && (blocks.readAt != 0))
                        {
                            writeAt = 0;
                            num9 = (writeAt < blocks.readAt) ? ((blocks.readAt - writeAt) - 1) : (blocks.end - writeAt);
                        }
                        if (num9 == 0)
                        {
                            blocks.bitb = bitb;
                            blocks.bitk = bitk;
                            z.AvailableBytesIn = availableBytesIn;
                            z.TotalBytesIn += nextIn - z.NextIn;
                            z.NextIn = nextIn;
                            blocks.writeAt = writeAt;
                            return blocks.Flush(r);
                        }
                    }
                }
                blocks.window[writeAt++] = blocks.window[num10++];
                num9--;
                if (num10 == blocks.end)
                {
                    num10 = 0;
                }
                this.len--;
            }
            this.mode = 0;
            goto Label_0C48;
        Label_0B52:
            r = 1;
            blocks.bitb = bitb;
            blocks.bitk = bitk;
            z.AvailableBytesIn = availableBytesIn;
            z.TotalBytesIn += nextIn - z.NextIn;
            z.NextIn = nextIn;
            blocks.writeAt = writeAt;
            return blocks.Flush(r);
        Label_0C48:
            flag = true;
            switch (this.mode)
            {
                case 0:
                    if ((num9 < 0x102) || (availableBytesIn < 10))
                    {
                        goto Label_019A;
                    }
                    blocks.bitb = bitb;
                    blocks.bitk = bitk;
                    z.AvailableBytesIn = availableBytesIn;
                    z.TotalBytesIn += nextIn - z.NextIn;
                    z.NextIn = nextIn;
                    blocks.writeAt = writeAt;
                    r = this.InflateFast(this.lbits, this.dbits, this.ltree, this.ltree_index, this.dtree, this.dtree_index, blocks, z);
                    nextIn = z.NextIn;
                    availableBytesIn = z.AvailableBytesIn;
                    bitb = blocks.bitb;
                    bitk = blocks.bitk;
                    writeAt = blocks.writeAt;
                    num9 = (writeAt < blocks.readAt) ? ((blocks.readAt - writeAt) - 1) : (blocks.end - writeAt);
                    if (r == 0)
                    {
                        goto Label_019A;
                    }
                    this.mode = (r == 1) ? 7 : 9;
                    goto Label_0C48;

                case 1:
                    goto Label_01C7;

                case 2:
                    bitsToGet = this.bitsToGet;
                    while (bitk < bitsToGet)
                    {
                        if (availableBytesIn != 0)
                        {
                            r = 0;
                        }
                        else
                        {
                            blocks.bitb = bitb;
                            blocks.bitk = bitk;
                            z.AvailableBytesIn = availableBytesIn;
                            z.TotalBytesIn += nextIn - z.NextIn;
                            z.NextIn = nextIn;
                            blocks.writeAt = writeAt;
                            return blocks.Flush(r);
                        }
                        availableBytesIn--;
                        bitb |= (z.InputBuffer[nextIn++] & 0xff) << bitk;
                        bitk += 8;
                    }
                    this.len += bitb & InternalInflateConstants.InflateMask[bitsToGet];
                    bitb = bitb >> bitsToGet;
                    bitk -= bitsToGet;
                    this.need = this.dbits;
                    this.tree = this.dtree;
                    this.tree_index = this.dtree_index;
                    this.mode = 3;
                    goto Label_04B1;

                case 3:
                    goto Label_04B1;

                case 4:
                    bitsToGet = this.bitsToGet;
                    while (bitk < bitsToGet)
                    {
                        if (availableBytesIn != 0)
                        {
                            r = 0;
                        }
                        else
                        {
                            blocks.bitb = bitb;
                            blocks.bitk = bitk;
                            z.AvailableBytesIn = availableBytesIn;
                            z.TotalBytesIn += nextIn - z.NextIn;
                            z.NextIn = nextIn;
                            blocks.writeAt = writeAt;
                            return blocks.Flush(r);
                        }
                        availableBytesIn--;
                        bitb |= (z.InputBuffer[nextIn++] & 0xff) << bitk;
                        bitk += 8;
                    }
                    this.dist += bitb & InternalInflateConstants.InflateMask[bitsToGet];
                    bitb = bitb >> bitsToGet;
                    bitk -= bitsToGet;
                    this.mode = 5;
                    goto Label_0733;

                case 5:
                    goto Label_0733;

                case 6:
                    if (num9 == 0)
                    {
                        if ((writeAt == blocks.end) && (blocks.readAt != 0))
                        {
                            writeAt = 0;
                            num9 = (writeAt < blocks.readAt) ? ((blocks.readAt - writeAt) - 1) : (blocks.end - writeAt);
                        }
                        if (num9 == 0)
                        {
                            blocks.writeAt = writeAt;
                            r = blocks.Flush(r);
                            writeAt = blocks.writeAt;
                            num9 = (writeAt < blocks.readAt) ? ((blocks.readAt - writeAt) - 1) : (blocks.end - writeAt);
                            if ((writeAt == blocks.end) && (blocks.readAt != 0))
                            {
                                writeAt = 0;
                                num9 = (writeAt < blocks.readAt) ? ((blocks.readAt - writeAt) - 1) : (blocks.end - writeAt);
                            }
                            if (num9 == 0)
                            {
                                blocks.bitb = bitb;
                                blocks.bitk = bitk;
                                z.AvailableBytesIn = availableBytesIn;
                                z.TotalBytesIn += nextIn - z.NextIn;
                                z.NextIn = nextIn;
                                blocks.writeAt = writeAt;
                                return blocks.Flush(r);
                            }
                        }
                    }
                    r = 0;
                    blocks.window[writeAt++] = (byte) this.lit;
                    num9--;
                    this.mode = 0;
                    goto Label_0C48;

                case 7:
                    if (bitk > 7)
                    {
                        bitk -= 8;
                        availableBytesIn++;
                        nextIn--;
                    }
                    blocks.writeAt = writeAt;
                    r = blocks.Flush(r);
                    writeAt = blocks.writeAt;
                    num9 = (writeAt < blocks.readAt) ? ((blocks.readAt - writeAt) - 1) : (blocks.end - writeAt);
                    if (blocks.readAt != blocks.writeAt)
                    {
                        blocks.bitb = bitb;
                        blocks.bitk = bitk;
                        z.AvailableBytesIn = availableBytesIn;
                        z.TotalBytesIn += nextIn - z.NextIn;
                        z.NextIn = nextIn;
                        blocks.writeAt = writeAt;
                        return blocks.Flush(r);
                    }
                    this.mode = 8;
                    goto Label_0B52;

                case 8:
                    goto Label_0B52;

                case 9:
                    r = -3;
                    blocks.bitb = bitb;
                    blocks.bitk = bitk;
                    z.AvailableBytesIn = availableBytesIn;
                    z.TotalBytesIn += nextIn - z.NextIn;
                    z.NextIn = nextIn;
                    blocks.writeAt = writeAt;
                    return blocks.Flush(r);
            }
            r = -2;
            blocks.bitb = bitb;
            blocks.bitk = bitk;
            z.AvailableBytesIn = availableBytesIn;
            z.TotalBytesIn += nextIn - z.NextIn;
            z.NextIn = nextIn;
            blocks.writeAt = writeAt;
            return blocks.Flush(r);
        }
    }
}

