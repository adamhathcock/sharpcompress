namespace SharpCompress.Compressor.Rar
{
    using SharpCompress;
    using SharpCompress.Compressor.Rar.decode;
    using SharpCompress.Compressor.Rar.VM;
    using System;
    using System.IO;

    internal abstract class Unpack15 : BitInput
    {
        protected internal int AvrLn1;
        protected internal int AvrLn2;
        protected internal int AvrLn3;
        protected internal int AvrPlc;
        protected internal int AvrPlcB;
        protected internal int Buf60;
        protected internal int[] ChSet = new int[0x100];
        protected internal int[] ChSetA = new int[0x100];
        protected internal int[] ChSetB = new int[0x100];
        protected internal int[] ChSetC = new int[0x100];
        private static int[] DecHf0 = new int[] { 0x8000, 0xc000, 0xe000, 0xf200, 0xf200, 0xf200, 0xf200, 0xf200, 0xffff };
        private static int[] DecHf1 = new int[] { 0x2000, 0xc000, 0xe000, 0xf000, 0xf200, 0xf200, 0xf7e0, 0xffff };
        private static int[] DecHf2 = new int[] { 0x1000, 0x2400, 0x8000, 0xc000, 0xfa00, 0xffff, 0xffff, 0xffff };
        private static int[] DecHf3 = new int[] { 0x800, 0x2400, 0xee00, 0xfe80, 0xffff, 0xffff, 0xffff };
        private static int[] DecHf4 = new int[] { 0xff00, 0xffff, 0xffff, 0xffff, 0xffff, 0xffff };
        private static int[] DecL1 = new int[] { 0x8000, 0xa000, 0xc000, 0xd000, 0xe000, 0xea00, 0xee00, 0xf000, 0xf200, 0xf200, 0xffff };
        private static int[] DecL2 = new int[] { 0xa000, 0xc000, 0xd000, 0xe000, 0xea00, 0xee00, 0xf000, 0xf200, 0xf240, 0xffff };
        protected internal long destUnpSize;
        protected internal int FlagBuf;
        protected internal int FlagsCnt;
        protected internal int lastDist;
        protected internal int lastLength;
        protected internal int LCount;
        protected internal int MaxDist3;
        protected internal int Nhfb;
        protected internal int Nlzb;
        protected internal int[] NToPl = new int[0x100];
        protected internal int[] NToPlB = new int[0x100];
        protected internal int[] NToPlC = new int[0x100];
        protected internal int NumHuf;
        protected internal int[] oldDist = new int[4];
        protected internal int oldDistPtr;
        protected internal int[] Place = new int[0x100];
        protected internal int[] PlaceA = new int[0x100];
        protected internal int[] PlaceB = new int[0x100];
        protected internal int[] PlaceC = new int[0x100];
        private static int[] PosHf0 = new int[] { 0, 0, 0, 0, 0, 8, 0x10, 0x18, 0x21, 0x21, 0x21, 0x21, 0x21 };
        private static int[] PosHf1 = new int[] { 0, 0, 0, 0, 0, 0, 4, 0x2c, 60, 0x4c, 80, 80, 0x7f };
        private static int[] PosHf2 = new int[] { 0, 0, 0, 0, 0, 0, 2, 7, 0x35, 0x75, 0xe9, 0, 0 };
        private static int[] PosHf3 = new int[] { 0, 0, 0, 0, 0, 0, 0, 2, 0x10, 0xda, 0xfb, 0, 0 };
        private static int[] PosHf4;
        private static int[] PosL1 = new int[] { 0, 0, 0, 2, 3, 5, 7, 11, 0x10, 20, 0x18, 0x20, 0x20 };
        private static int[] PosL2 = new int[] { 0, 0, 0, 0, 5, 7, 9, 13, 0x12, 0x16, 0x1a, 0x22, 0x24 };
        protected internal int readBorder;
        protected Stream readStream;
        protected internal int readTop;
        internal static int[] ShortLen1;
        internal static int[] ShortLen2;
        internal static int[] ShortXor1;
        internal static int[] ShortXor2;
        private const int STARTHF0 = 4;
        private const int STARTHF1 = 5;
        private const int STARTHF2 = 5;
        private const int STARTHF3 = 6;
        private const int STARTHF4 = 8;
        private const int STARTL1 = 2;
        private const int STARTL2 = 3;
        protected internal int StMode;
        protected internal bool suspended;
        protected internal bool unpAllBuf;
        protected internal int unpPtr;
        protected internal bool unpSomeRead;
        protected internal byte[] window;
        protected Stream writeStream;
        protected internal int wrPtr;

        static Unpack15()
        {
            int[] numArray = new int[13];
            numArray[9] = 0xff;
            PosHf4 = numArray;
            ShortLen1 = new int[] { 1, 3, 4, 4, 5, 6, 7, 8, 8, 4, 4, 5, 6, 6, 4, 0 };
            ShortXor1 = new int[] { 0, 160, 0xd0, 0xe0, 240, 0xf8, 0xfc, 0xfe, 0xff, 0xc0, 0x80, 0x90, 0x98, 0x9c, 0xb0 };
            ShortLen2 = new int[] { 2, 3, 3, 3, 4, 4, 5, 6, 6, 4, 4, 5, 6, 6, 4, 0 };
            ShortXor2 = new int[] { 0, 0x40, 0x60, 160, 0xd0, 0xe0, 240, 0xf8, 0xfc, 0xc0, 0x80, 0x90, 0x98, 0x9c, 0xb0 };
        }

        protected Unpack15()
        {
        }

        private void corrHuff(int[] CharSet, int[] NumToPlace)
        {
            int num;
            int index = 0;
            for (num = 7; num >= 0; num--)
            {
                int num2 = 0;
                while (num2 < 0x20)
                {
                    CharSet[index] = (CharSet[index] & -256) | num;
                    num2++;
                    index++;
                }
            }
            Utility.Fill<int>(NumToPlace, 0);
            for (num = 6; num >= 0; num--)
            {
                NumToPlace[num] = (7 - num) * 0x20;
            }
        }

        private int decodeNum(int Num, int StartPos, int[] DecTab, int[] PosTab)
        {
            Num &= 0xfff0;
            int index = 0;
            while (DecTab[index] <= Num)
            {
                StartPos++;
                index++;
            }
            base.AddBits(StartPos);
            return (Utility.URShift((int) (Num - ((index != 0) ? DecTab[index - 1] : 0)), (int) (0x10 - StartPos)) + PosTab[StartPos]);
        }

        private void getFlagsBuf()
        {
            int index = this.decodeNum(base.GetBits(), 5, DecHf2, PosHf2);
            while (true)
            {
                int number = this.ChSetC[index];
                this.FlagBuf = Utility.URShift(number, 8);
                int num2 = this.NToPlC[number++ & 0xff]++;
                if ((number & 0xff) != 0)
                {
                    this.ChSetC[index] = this.ChSetC[num2];
                    this.ChSetC[num2] = number;
                    return;
                }
                this.corrHuff(this.ChSetC, this.NToPlC);
            }
        }

        private int getShortLen1(int pos)
        {
            return ((pos == 1) ? (this.Buf60 + 3) : ShortLen1[pos]);
        }

        private int getShortLen2(int pos)
        {
            return ((pos == 3) ? (this.Buf60 + 3) : ShortLen2[pos]);
        }

        private void huffDecode()
        {
            int num5;
            int bits = base.GetBits();
            if (this.AvrPlc > 0x75ff)
            {
                num5 = this.decodeNum(bits, 8, DecHf4, PosHf4);
            }
            else if (this.AvrPlc > 0x5dff)
            {
                num5 = this.decodeNum(bits, 6, DecHf3, PosHf3);
            }
            else if (this.AvrPlc > 0x35ff)
            {
                num5 = this.decodeNum(bits, 5, DecHf2, PosHf2);
            }
            else if (this.AvrPlc > 0xdff)
            {
                num5 = this.decodeNum(bits, 5, DecHf1, PosHf1);
            }
            else
            {
                num5 = this.decodeNum(bits, 4, DecHf0, PosHf0);
            }
            num5 &= 0xff;
            if (this.StMode != 0)
            {
                if ((num5 == 0) && (bits > 0xfff))
                {
                    num5 = 0x100;
                }
                if (--num5 == -1)
                {
                    bits = base.GetBits();
                    base.AddBits(1);
                    if ((bits & 0x8000) != 0)
                    {
                        this.NumHuf = this.StMode = 0;
                    }
                    else
                    {
                        int length = ((bits & 0x4000) != 0) ? 4 : 3;
                        base.AddBits(1);
                        int distance = (this.decodeNum(base.GetBits(), 5, DecHf2, PosHf2) << 5) | Utility.URShift(base.GetBits(), 11);
                        base.AddBits(5);
                        this.oldCopyString(distance, length);
                    }
                    return;
                }
            }
            else if ((this.NumHuf++ >= 0x10) && (this.FlagsCnt == 0))
            {
                this.StMode = 1;
            }
            this.AvrPlc += num5;
            this.AvrPlc -= Utility.URShift(this.AvrPlc, 8);
            this.Nhfb += 0x10;
            if (this.Nhfb > 0xff)
            {
                this.Nhfb = 0x90;
                this.Nlzb = Utility.URShift(this.Nlzb, 1);
            }
            this.window[this.unpPtr++] = (byte) Utility.URShift(this.ChSet[num5], 8);
            this.destUnpSize -= 1L;
            while (true)
            {
                int num = this.ChSet[num5];
                int index = this.NToPl[num++ & 0xff]++;
                if ((num & 0xff) > 0xa1)
                {
                    this.corrHuff(this.ChSet, this.NToPl);
                }
                else
                {
                    this.ChSet[num5] = this.ChSet[index];
                    this.ChSet[index] = num;
                    return;
                }
            }
        }

        private void initHuff()
        {
            for (int i = 0; i < 0x100; i++)
            {
                int num2;
                this.PlaceB[i] = num2 = i;
                this.Place[i] = this.PlaceA[i] = num2;
                this.PlaceC[i] = (~i + 1) & 0xff;
                this.ChSet[i] = this.ChSetB[i] = i << 8;
                this.ChSetA[i] = i;
                this.ChSetC[i] = ((~i + 1) & 0xff) << 8;
            }
            Utility.Fill<int>(this.NToPl, 0);
            Utility.Fill<int>(this.NToPlB, 0);
            Utility.Fill<int>(this.NToPlC, 0);
            this.corrHuff(this.ChSetB, this.NToPlB);
        }

        private void longLZ()
        {
            int num;
            int num3;
            this.NumHuf = 0;
            this.Nlzb += 0x10;
            if (this.Nlzb > 0xff)
            {
                this.Nlzb = 0x90;
                this.Nhfb = Utility.URShift(this.Nhfb, 1);
            }
            int num5 = this.AvrLn2;
            int bits = base.GetBits();
            if (this.AvrLn2 >= 0x7a)
            {
                num = this.decodeNum(bits, 3, DecL2, PosL2);
            }
            else if (this.AvrLn2 >= 0x40)
            {
                num = this.decodeNum(bits, 2, DecL1, PosL1);
            }
            else if (bits < 0x100)
            {
                num = bits;
                base.AddBits(0x10);
            }
            else
            {
                num = 0;
                while (((bits << num) & 0x8000) == 0)
                {
                    num++;
                }
                base.AddBits(num + 1);
            }
            this.AvrLn2 += num;
            this.AvrLn2 -= Utility.URShift(this.AvrLn2, 5);
            bits = base.GetBits();
            if (this.AvrPlcB > 0x28ff)
            {
                num3 = this.decodeNum(bits, 5, DecHf2, PosHf2);
            }
            else if (this.AvrPlcB > 0x6ff)
            {
                num3 = this.decodeNum(bits, 5, DecHf1, PosHf1);
            }
            else
            {
                num3 = this.decodeNum(bits, 4, DecHf0, PosHf0);
            }
            this.AvrPlcB += num3;
            this.AvrPlcB -= this.AvrPlcB >> 8;
            while (true)
            {
                int distance = this.ChSetB[num3 & 0xff];
                int index = this.NToPlB[distance++ & 0xff]++;
                if ((distance & 0xff) == 0)
                {
                    this.corrHuff(this.ChSetB, this.NToPlB);
                }
                else
                {
                    this.ChSetB[num3] = this.ChSetB[index];
                    this.ChSetB[index] = distance;
                    distance = Utility.URShift((int) ((distance & 0xff00) | Utility.URShift(base.GetBits(), 8)), 1);
                    base.AddBits(7);
                    int num6 = this.AvrLn3;
                    if ((num != 1) && (num != 4))
                    {
                        if ((num == 0) && (distance <= this.MaxDist3))
                        {
                            this.AvrLn3++;
                            this.AvrLn3 -= this.AvrLn3 >> 8;
                        }
                        else if (this.AvrLn3 > 0)
                        {
                            this.AvrLn3--;
                        }
                    }
                    num += 3;
                    if (distance >= this.MaxDist3)
                    {
                        num++;
                    }
                    if (distance <= 0x100)
                    {
                        num += 8;
                    }
                    if ((num6 > 0xb0) || ((this.AvrPlc >= 0x2a00) && (num5 < 0x40)))
                    {
                        this.MaxDist3 = 0x7f00;
                    }
                    else
                    {
                        this.MaxDist3 = 0x2001;
                    }
                    this.oldDist[this.oldDistPtr++] = distance;
                    this.oldDistPtr &= 3;
                    this.lastLength = num;
                    this.lastDist = distance;
                    this.oldCopyString(distance, num);
                    return;
                }
            }
        }

        private void oldCopyString(int Distance, int Length)
        {
            this.destUnpSize -= Length;
            while (Length-- != 0)
            {
                this.window[this.unpPtr] = this.window[(this.unpPtr - Distance) & Compress.MAXWINMASK];
                this.unpPtr = (this.unpPtr + 1) & Compress.MAXWINMASK;
            }
        }

        private void oldUnpInitData(bool Solid)
        {
            if (!Solid)
            {
                this.AvrPlcB = this.AvrLn1 = this.AvrLn2 = this.AvrLn3 = this.NumHuf = this.Buf60 = 0;
                this.AvrPlc = 0x3500;
                this.MaxDist3 = 0x2001;
                this.Nhfb = this.Nlzb = 0x80;
            }
            this.FlagsCnt = 0;
            this.FlagBuf = 0;
            this.StMode = 0;
            this.LCount = 0;
            this.readTop = 0;
        }

        protected void oldUnpWriteBuf()
        {
            if (this.unpPtr != this.wrPtr)
            {
                this.unpSomeRead = true;
            }
            if (this.unpPtr < this.wrPtr)
            {
                this.writeStream.Write(this.window, this.wrPtr, -this.wrPtr & Compress.MAXWINMASK);
                this.writeStream.Write(this.window, 0, this.unpPtr);
                this.unpAllBuf = true;
            }
            else
            {
                this.writeStream.Write(this.window, this.wrPtr, this.unpPtr - this.wrPtr);
            }
            this.wrPtr = this.unpPtr;
        }

        private void shortLZ()
        {
            int num;
            int num4;
            bool flag;
            this.NumHuf = 0;
            int bits = base.GetBits();
            if (this.LCount == 2)
            {
                base.AddBits(1);
                if (bits >= 0x8000)
                {
                    this.oldCopyString(this.lastDist, this.lastLength);
                    return;
                }
                bits = bits << 1;
                this.LCount = 0;
            }
            bits = Utility.URShift(bits, 8);
            if (this.AvrLn1 >= 0x25)
            {
                num = 0;
            }
            else
            {
                num = 0;
                while (true)
                {
                    flag = true;
                    if (((bits ^ ShortXor1[num]) & ~Utility.URShift(0xff, this.getShortLen1(num))) == 0)
                    {
                        base.AddBits(this.getShortLen1(num));
                        goto Label_0115;
                    }
                    num++;
                }
            }
        Label_0101:
            flag = true;
            if (((bits ^ ShortXor2[num]) & ~(((int) 0xff) >> this.getShortLen2(num))) == 0)
            {
                base.AddBits(this.getShortLen2(num));
            }
            else
            {
                num++;
                goto Label_0101;
            }
        Label_0115:
            if (num >= 9)
            {
                if (num == 9)
                {
                    this.LCount++;
                    this.oldCopyString(this.lastDist, this.lastLength);
                }
                else if (num == 14)
                {
                    this.LCount = 0;
                    num = this.decodeNum(base.GetBits(), 3, DecL2, PosL2) + 5;
                    num4 = (base.GetBits() >> 1) | 0x8000;
                    base.AddBits(15);
                    this.lastLength = num;
                    this.lastDist = num4;
                    this.oldCopyString(num4, num);
                }
                else
                {
                    this.LCount = 0;
                    int num2 = num;
                    num4 = this.oldDist[(this.oldDistPtr - (num - 9)) & 3];
                    num = this.decodeNum(base.GetBits(), 2, DecL1, PosL1) + 2;
                    if ((num == 0x101) && (num2 == 10))
                    {
                        this.Buf60 ^= 1;
                    }
                    else
                    {
                        if (num4 > 0x100)
                        {
                            num++;
                        }
                        if (num4 >= this.MaxDist3)
                        {
                            num++;
                        }
                        this.oldDist[this.oldDistPtr++] = num4;
                        this.oldDistPtr &= 3;
                        this.lastLength = num;
                        this.lastDist = num4;
                        this.oldCopyString(num4, num);
                    }
                }
            }
            else
            {
                this.LCount = 0;
                this.AvrLn1 += num;
                this.AvrLn1 -= this.AvrLn1 >> 4;
                int index = this.decodeNum(base.GetBits(), 5, DecHf2, PosHf2) & 0xff;
                num4 = this.ChSetA[index];
                if (--index != -1)
                {
                    this.PlaceA[num4]--;
                    int num3 = this.ChSetA[index];
                    this.PlaceA[num3]++;
                    this.ChSetA[index + 1] = num3;
                    this.ChSetA[index] = num4;
                }
                num += 2;
                this.oldDist[this.oldDistPtr++] = ++num4;
                this.oldDistPtr &= 3;
                this.lastLength = num;
                this.lastDist = num4;
                this.oldCopyString(num4, num);
            }
        }

        protected void unpack15(bool solid)
        {
            if (this.suspended)
            {
                this.unpPtr = this.wrPtr;
            }
            else
            {
                this.unpInitData(solid);
                this.oldUnpInitData(solid);
                this.unpReadBuf();
                if (!solid)
                {
                    this.initHuff();
                    this.unpPtr = 0;
                }
                else
                {
                    this.unpPtr = this.wrPtr;
                }
                this.destUnpSize -= 1L;
            }
            if (this.destUnpSize >= 0L)
            {
                this.getFlagsBuf();
                this.FlagsCnt = 8;
            }
            while (this.destUnpSize >= 0L)
            {
                this.unpPtr &= Compress.MAXWINMASK;
                if (!((base.inAddr <= (this.readTop - 30)) || this.unpReadBuf()))
                {
                    break;
                }
                if ((((this.wrPtr - this.unpPtr) & Compress.MAXWINMASK) < 270) && (this.wrPtr != this.unpPtr))
                {
                    this.oldUnpWriteBuf();
                    if (this.suspended)
                    {
                        return;
                    }
                }
                if (this.StMode != 0)
                {
                    this.huffDecode();
                }
                else
                {
                    if (--this.FlagsCnt < 0)
                    {
                        this.getFlagsBuf();
                        this.FlagsCnt = 7;
                    }
                    if ((this.FlagBuf & 0x80) != 0)
                    {
                        this.FlagBuf = this.FlagBuf << 1;
                        if (this.Nlzb > this.Nhfb)
                        {
                            this.longLZ();
                        }
                        else
                        {
                            this.huffDecode();
                        }
                    }
                    else
                    {
                        this.FlagBuf = this.FlagBuf << 1;
                        if (--this.FlagsCnt < 0)
                        {
                            this.getFlagsBuf();
                            this.FlagsCnt = 7;
                        }
                        if ((this.FlagBuf & 0x80) != 0)
                        {
                            this.FlagBuf = this.FlagBuf << 1;
                            if (this.Nlzb > this.Nhfb)
                            {
                                this.huffDecode();
                            }
                            else
                            {
                                this.longLZ();
                            }
                        }
                        else
                        {
                            this.FlagBuf = this.FlagBuf << 1;
                            this.shortLZ();
                        }
                    }
                }
            }
            this.oldUnpWriteBuf();
        }

        protected internal abstract void unpInitData(bool solid);
        protected bool unpReadBuf()
        {
            int length = this.readTop - base.inAddr;
            if (length < 0)
            {
                return false;
            }
            if (base.inAddr > 0x4000)
            {
                if (length > 0)
                {
                    Array.Copy(base.InBuf, base.inAddr, base.InBuf, 0, length);
                }
                base.inAddr = 0;
                this.readTop = length;
            }
            else
            {
                length = this.readTop;
            }
            int num2 = this.readStream.Read(base.InBuf, length, (0x8000 - length) & -16);
            if (num2 > 0)
            {
                this.readTop += num2;
            }
            this.readBorder = this.readTop - 30;
            return (num2 != -1);
        }
    }
}

