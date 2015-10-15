namespace SharpCompress.Compressor.BZip2
{
    using System;
    using System.IO;

    internal class CBZip2OutputStream : Stream
    {
        private int allowableBlockSize;
        private char[] block;
        private int blockCRC;
        private bool blockRandomised;
        private int blockSize100k;
        private int bsBuff;
        private int bsLive;
        private Stream bsStream;
        private int bytesOut;
        protected const int CLEARMASK = -2097153;
        private int combinedCRC;
        private int currentChar;
        protected const int DEPTH_THRESH = 10;
        private bool disposed;
        private bool finished;
        private bool firstAttempt;
        private int[] ftab;
        protected const int GREATER_ICOST = 15;
        private int[] incs;
        private bool[] inUse;
        private int last;
        private bool leaveOpen;
        protected const int LESSER_ICOST = 0;
        private CRC mCrc;
        private int[] mtfFreq;
        private int nBlocksRandomised;
        private int nInUse;
        private int nMTF;
        private int origPtr;
        protected const int QSORT_STACK_SIZE = 0x3e8;
        private int[] quadrant;
        private int runLength;
        private char[] selector;
        private char[] selectorMtf;
        private char[] seqToUnseq;
        protected const int SETMASK = 0x200000;
        protected const int SMALL_THRESH = 20;
        private short[] szptr;
        private char[] unseqToSeq;
        private int workDone;
        private int workFactor;
        private int workLimit;
        private int[] zptr;

        public CBZip2OutputStream(Stream inStream) : this(inStream, 9, false)
        {
        }

        public CBZip2OutputStream(Stream inStream, bool leaveOpen) : this(inStream, 9, leaveOpen)
        {
        }

        public CBZip2OutputStream(Stream inStream, int inBlockSize, bool leaveOpen)
        {
            this.mCrc = new CRC();
            this.inUse = new bool[0x100];
            this.seqToUnseq = new char[0x100];
            this.unseqToSeq = new char[0x100];
            this.selector = new char[0x4652];
            this.selectorMtf = new char[0x4652];
            this.mtfFreq = new int[0x102];
            this.currentChar = -1;
            this.runLength = 0;
            this.disposed = false;
            this.incs = new int[] { 1, 4, 13, 40, 0x79, 0x16c, 0x445, 0xcd0, 0x2671, 0x7354, 0x159fd, 0x40df8, 0xc29e9, 0x247dbc };
            this.block = null;
            this.quadrant = null;
            this.zptr = null;
            this.ftab = null;
            inStream.WriteByte(0x42);
            inStream.WriteByte(90);
            this.BsSetStream(inStream, leaveOpen);
            this.workFactor = 50;
            if (inBlockSize > 9)
            {
                inBlockSize = 9;
            }
            if (inBlockSize < 1)
            {
                inBlockSize = 1;
            }
            this.blockSize100k = inBlockSize;
            this.AllocateCompressStructures();
            this.Initialize();
            this.InitBlock();
        }

        private void AllocateCompressStructures()
        {
            int num = 0x186a0 * this.blockSize100k;
            this.block = new char[(num + 1) + 20];
            this.quadrant = new int[num + 20];
            this.zptr = new int[num];
            this.ftab = new int[0x10001];
            if ((((this.block == null) || (this.quadrant == null)) || (this.zptr == null)) || (this.ftab == null))
            {
            }
            this.szptr = new short[2 * num];
        }

        private void BsFinishedWithStream()
        {
            while (this.bsLive > 0)
            {
                int num = this.bsBuff >> 0x18;
                try
                {
                    this.bsStream.WriteByte((byte) num);
                }
                catch (IOException exception)
                {
                    throw exception;
                }
                this.bsBuff = this.bsBuff << 8;
                this.bsLive -= 8;
                this.bytesOut++;
            }
        }

        private void BsPutint(int u)
        {
            this.BsW(8, (u >> 0x18) & 0xff);
            this.BsW(8, (u >> 0x10) & 0xff);
            this.BsW(8, (u >> 8) & 0xff);
            this.BsW(8, u & 0xff);
        }

        private void BsPutIntVS(int numBits, int c)
        {
            this.BsW(numBits, c);
        }

        private void BsPutUChar(int c)
        {
            this.BsW(8, c);
        }

        private void BsSetStream(Stream f, bool leaveOpen)
        {
            this.bsStream = f;
            this.bsLive = 0;
            this.bsBuff = 0;
            this.bytesOut = 0;
            this.leaveOpen = leaveOpen;
        }

        private void BsW(int n, int v)
        {
            while (this.bsLive >= 8)
            {
                int num = this.bsBuff >> 0x18;
                try
                {
                    this.bsStream.WriteByte((byte) num);
                }
                catch (IOException exception)
                {
                    throw exception;
                }
                this.bsBuff = this.bsBuff << 8;
                this.bsLive -= 8;
                this.bytesOut++;
            }
            this.bsBuff |= v << ((0x20 - this.bsLive) - n);
            this.bsLive += n;
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing && !this.disposed)
            {
                this.Finish();
                this.disposed = true;
                base.Dispose();
                if (!this.leaveOpen)
                {
                    this.bsStream.Dispose();
                }
                this.bsStream = null;
            }
        }

        private void DoReversibleTransformation()
        {
            this.workLimit = this.workFactor * this.last;
            this.workDone = 0;
            this.blockRandomised = false;
            this.firstAttempt = true;
            this.MainSort();
            if ((this.workDone > this.workLimit) && this.firstAttempt)
            {
                this.RandomiseBlock();
                this.workLimit = this.workDone = 0;
                this.blockRandomised = true;
                this.firstAttempt = false;
                this.MainSort();
            }
            this.origPtr = -1;
            for (int i = 0; i <= this.last; i++)
            {
                if (this.zptr[i] == 0)
                {
                    this.origPtr = i;
                    break;
                }
            }
            if (this.origPtr == -1)
            {
                Panic();
            }
        }

        private void EndBlock()
        {
            this.blockCRC = this.mCrc.GetFinalCRC();
            this.combinedCRC = (this.combinedCRC << 1) | (this.combinedCRC >> 0x1f);
            this.combinedCRC ^= this.blockCRC;
            this.DoReversibleTransformation();
            this.BsPutUChar(0x31);
            this.BsPutUChar(0x41);
            this.BsPutUChar(0x59);
            this.BsPutUChar(0x26);
            this.BsPutUChar(0x53);
            this.BsPutUChar(0x59);
            this.BsPutint(this.blockCRC);
            if (this.blockRandomised)
            {
                this.BsW(1, 1);
                this.nBlocksRandomised++;
            }
            else
            {
                this.BsW(1, 0);
            }
            this.MoveToFrontCodeAndSend();
        }

        private void EndCompression()
        {
            this.BsPutUChar(0x17);
            this.BsPutUChar(0x72);
            this.BsPutUChar(0x45);
            this.BsPutUChar(0x38);
            this.BsPutUChar(80);
            this.BsPutUChar(0x90);
            this.BsPutint(this.combinedCRC);
            this.BsFinishedWithStream();
        }

        public void Finish()
        {
            if (!this.finished)
            {
                if (this.runLength > 0)
                {
                    this.WriteRun();
                }
                this.currentChar = -1;
                this.EndBlock();
                this.EndCompression();
                this.finished = true;
                this.Flush();
            }
        }

        public override void Flush()
        {
            this.bsStream.Flush();
        }

        private bool FullGtU(int i1, int i2)
        {
            char ch = this.block[i1 + 1];
            char ch2 = this.block[i2 + 1];
            if (ch != ch2)
            {
                return (ch > ch2);
            }
            i1++;
            i2++;
            ch = this.block[i1 + 1];
            ch2 = this.block[i2 + 1];
            if (ch != ch2)
            {
                return (ch > ch2);
            }
            i1++;
            i2++;
            ch = this.block[i1 + 1];
            ch2 = this.block[i2 + 1];
            if (ch != ch2)
            {
                return (ch > ch2);
            }
            i1++;
            i2++;
            ch = this.block[i1 + 1];
            ch2 = this.block[i2 + 1];
            if (ch != ch2)
            {
                return (ch > ch2);
            }
            i1++;
            i2++;
            ch = this.block[i1 + 1];
            ch2 = this.block[i2 + 1];
            if (ch != ch2)
            {
                return (ch > ch2);
            }
            i1++;
            i2++;
            ch = this.block[i1 + 1];
            ch2 = this.block[i2 + 1];
            if (ch != ch2)
            {
                return (ch > ch2);
            }
            i1++;
            i2++;
            int num = this.last + 1;
            do
            {
                ch = this.block[i1 + 1];
                ch2 = this.block[i2 + 1];
                if (ch != ch2)
                {
                    return (ch > ch2);
                }
                int num2 = this.quadrant[i1];
                int num3 = this.quadrant[i2];
                if (num2 != num3)
                {
                    return (num2 > num3);
                }
                i1++;
                i2++;
                ch = this.block[i1 + 1];
                ch2 = this.block[i2 + 1];
                if (ch != ch2)
                {
                    return (ch > ch2);
                }
                num2 = this.quadrant[i1];
                num3 = this.quadrant[i2];
                if (num2 != num3)
                {
                    return (num2 > num3);
                }
                i1++;
                i2++;
                ch = this.block[i1 + 1];
                ch2 = this.block[i2 + 1];
                if (ch != ch2)
                {
                    return (ch > ch2);
                }
                num2 = this.quadrant[i1];
                num3 = this.quadrant[i2];
                if (num2 != num3)
                {
                    return (num2 > num3);
                }
                i1++;
                i2++;
                ch = this.block[i1 + 1];
                ch2 = this.block[i2 + 1];
                if (ch != ch2)
                {
                    return (ch > ch2);
                }
                num2 = this.quadrant[i1];
                num3 = this.quadrant[i2];
                if (num2 != num3)
                {
                    return (num2 > num3);
                }
                i1++;
                i2++;
                if (i1 > this.last)
                {
                    i1 -= this.last;
                    i1--;
                }
                if (i2 > this.last)
                {
                    i2 -= this.last;
                    i2--;
                }
                num -= 4;
                this.workDone++;
            }
            while (num >= 0);
            return false;
        }

        private void GenerateMTFValues()
        {
            int num;
            bool flag;
            char[] chArray = new char[0x100];
            this.MakeMaps();
            int index = this.nInUse + 1;
            for (num = 0; num <= index; num++)
            {
                this.mtfFreq[num] = 0;
            }
            int num4 = 0;
            int num3 = 0;
            for (num = 0; num < this.nInUse; num++)
            {
                chArray[num] = (char) num;
            }
            for (num = 0; num <= this.last; num++)
            {
                char ch3 = this.unseqToSeq[this.block[this.zptr[num]]];
                int num2 = 0;
                char ch = chArray[num2];
                while (ch3 != ch)
                {
                    num2++;
                    char ch2 = ch;
                    ch = chArray[num2];
                    chArray[num2] = ch2;
                }
                chArray[0] = ch;
                if (num2 == 0)
                {
                    num3++;
                    continue;
                }
                if (num3 <= 0)
                {
                    goto Label_017A;
                }
                num3--;
                goto Label_016E;
            Label_00E6:
                switch ((num3 % 2))
                {
                    case 0:
                        this.szptr[num4] = 0;
                        num4++;
                        this.mtfFreq[0]++;
                        break;

                    case 1:
                        this.szptr[num4] = 1;
                        num4++;
                        this.mtfFreq[1]++;
                        break;
                }
                if (num3 < 2)
                {
                    goto Label_0176;
                }
                num3 = (num3 - 2) / 2;
            Label_016E:
                flag = true;
                goto Label_00E6;
            Label_0176:
                num3 = 0;
            Label_017A:
                this.szptr[num4] = (short) (num2 + 1);
                num4++;
                this.mtfFreq[num2 + 1]++;
            }
            if (num3 <= 0)
            {
                goto Label_0271;
            }
            num3--;
            goto Label_0268;
        Label_024E:
            if (num3 < 2)
            {
                goto Label_0271;
            }
            num3 = (num3 - 2) / 2;
        Label_0268:
            flag = true;
            switch ((num3 % 2))
            {
                case 0:
                    this.szptr[num4] = 0;
                    num4++;
                    this.mtfFreq[0]++;
                    goto Label_024E;

                case 1:
                    this.szptr[num4] = 1;
                    num4++;
                    this.mtfFreq[1]++;
                    goto Label_024E;

                default:
                    goto Label_024E;
            }
        Label_0271:
            this.szptr[num4] = (short) index;
            num4++;
            this.mtfFreq[index]++;
            this.nMTF = num4;
        }

        private void HbAssignCodes(int[] code, char[] length, int minLen, int maxLen, int alphaSize)
        {
            int num2 = 0;
            for (int i = minLen; i <= maxLen; i++)
            {
                for (int j = 0; j < alphaSize; j++)
                {
                    if (length[j] == i)
                    {
                        code[j] = num2;
                        num2++;
                    }
                }
                num2 = num2 << 1;
            }
        }

        protected static void HbMakeCodeLengths(char[] len, int[] freq, int alphaSize, int maxLen)
        {
            int num5;
            int[] numArray = new int[260];
            int[] numArray2 = new int[0x204];
            int[] numArray3 = new int[0x204];
            for (num5 = 0; num5 < alphaSize; num5++)
            {
                numArray2[num5 + 1] = ((freq[num5] == null) ? 1 : freq[num5]) << 8;
            }
            while (true)
            {
                int num6;
                int num8;
                int num9;
                bool flag2 = true;
                int index = alphaSize;
                int num2 = 0;
                numArray[0] = 0;
                numArray2[0] = 0;
                numArray3[0] = -2;
                for (num5 = 1; num5 <= alphaSize; num5++)
                {
                    numArray3[num5] = -1;
                    num2++;
                    numArray[num2] = num5;
                    num8 = num2;
                    num9 = numArray[num8];
                    while (numArray2[num9] < numArray2[numArray[num8 >> 1]])
                    {
                        numArray[num8] = numArray[num8 >> 1];
                        num8 = num8 >> 1;
                    }
                    numArray[num8] = num9;
                }
                if (num2 >= 260)
                {
                    Panic();
                }
                while (num2 > 1)
                {
                    int num3 = numArray[1];
                    numArray[1] = numArray[num2];
                    num2--;
                    num8 = 0;
                    int num10 = 0;
                    num9 = 0;
                    num8 = 1;
                    num9 = numArray[num8];
                    goto Label_018C;
                Label_011C:
                    num10 = num8 << 1;
                    if (num10 > num2)
                    {
                        goto Label_0191;
                    }
                    if ((num10 < num2) && (numArray2[numArray[num10 + 1]] < numArray2[numArray[num10]]))
                    {
                        num10++;
                    }
                    if (numArray2[num9] < numArray2[numArray[num10]])
                    {
                        goto Label_0191;
                    }
                    numArray[num8] = numArray[num10];
                    num8 = num10;
                Label_018C:
                    flag2 = true;
                    goto Label_011C;
                Label_0191:
                    numArray[num8] = num9;
                    int num4 = numArray[1];
                    numArray[1] = numArray[num2];
                    num2--;
                    num8 = 0;
                    num10 = 0;
                    num9 = 0;
                    num8 = 1;
                    num9 = numArray[num8];
                    goto Label_0230;
                Label_01C0:
                    num10 = num8 << 1;
                    if (num10 > num2)
                    {
                        goto Label_0235;
                    }
                    if ((num10 < num2) && (numArray2[numArray[num10 + 1]] < numArray2[numArray[num10]]))
                    {
                        num10++;
                    }
                    if (numArray2[num9] < numArray2[numArray[num10]])
                    {
                        goto Label_0235;
                    }
                    numArray[num8] = numArray[num10];
                    num8 = num10;
                Label_0230:
                    flag2 = true;
                    goto Label_01C0;
                Label_0235:
                    numArray[num8] = num9;
                    index++;
                    numArray3[num3] = numArray3[num4] = index;
                    numArray2[index] = ((int) ((uint) ((numArray2[num3] & 0xffffff00L) + (numArray2[num4] & 0xffffff00L)))) | (1 + (((numArray2[num3] & 0xff) > (numArray2[num4] & 0xff)) ? (numArray2[num3] & 0xff) : (numArray2[num4] & 0xff)));
                    numArray3[index] = -1;
                    num2++;
                    numArray[num2] = index;
                    num8 = 0;
                    num9 = 0;
                    num8 = num2;
                    num9 = numArray[num8];
                    while (numArray2[num9] < numArray2[numArray[num8 >> 1]])
                    {
                        numArray[num8] = numArray[num8 >> 1];
                        num8 = num8 >> 1;
                    }
                    numArray[num8] = num9;
                }
                if (index >= 0x204)
                {
                    Panic();
                }
                bool flag = false;
                for (num5 = 1; num5 <= alphaSize; num5++)
                {
                    num6 = 0;
                    int num7 = num5;
                    while (numArray3[num7] >= 0)
                    {
                        num7 = numArray3[num7];
                        num6++;
                    }
                    len[num5 - 1] = (char) num6;
                    if (num6 > maxLen)
                    {
                        flag = true;
                    }
                }
                if (!flag)
                {
                    return;
                }
                for (num5 = 1; num5 < alphaSize; num5++)
                {
                    num6 = numArray2[num5] >> 8;
                    num6 = 1 + (num6 / 2);
                    numArray2[num5] = num6 << 8;
                }
            }
        }

        private void InitBlock()
        {
            this.mCrc.InitialiseCRC();
            this.last = -1;
            for (int i = 0; i < 0x100; i++)
            {
                this.inUse[i] = false;
            }
            this.allowableBlockSize = (0x186a0 * this.blockSize100k) - 20;
        }

        private void Initialize()
        {
            this.bytesOut = 0;
            this.nBlocksRandomised = 0;
            this.BsPutUChar(0x68);
            this.BsPutUChar(0x30 + this.blockSize100k);
            this.combinedCRC = 0;
        }

        private void MainSort()
        {
            int num;
            int[] numArray = new int[0x100];
            int[] numArray2 = new int[0x100];
            bool[] flagArray = new bool[0x100];
            for (num = 0; num < 20; num++)
            {
                this.block[(this.last + num) + 2] = this.block[(num % (this.last + 1)) + 1];
            }
            for (num = 0; num <= (this.last + 20); num++)
            {
                this.quadrant[num] = 0;
            }
            this.block[0] = this.block[this.last + 1];
            if (this.last < 0xfa0)
            {
                for (num = 0; num <= this.last; num++)
                {
                    this.zptr[num] = num;
                }
                this.firstAttempt = false;
                this.workDone = this.workLimit = 0;
                this.SimpleSort(0, this.last, 0);
            }
            else
            {
                int num2;
                int num6;
                int num7 = 0;
                for (num = 0; num <= 0xff; num++)
                {
                    flagArray[num] = false;
                }
                for (num = 0; num <= 0x10000; num++)
                {
                    this.ftab[num] = 0;
                }
                int index = this.block[0];
                for (num = 0; num <= this.last; num++)
                {
                    num6 = this.block[num + 1];
                    this.ftab[(index << 8) + num6]++;
                    index = num6;
                }
                for (num = 1; num <= 0x10000; num++)
                {
                    this.ftab[num] += this.ftab[num - 1];
                }
                index = this.block[1];
                for (num = 0; num < this.last; num++)
                {
                    num6 = this.block[num + 2];
                    num2 = (index << 8) + num6;
                    index = num6;
                    this.ftab[num2]--;
                    this.zptr[this.ftab[num2]] = num;
                }
                num2 = (this.block[this.last + 1] << 8) + this.block[1];
                this.ftab[num2]--;
                this.zptr[this.ftab[num2]] = this.last;
                num = 0;
                while (num <= 0xff)
                {
                    numArray[num] = num;
                    num++;
                }
                int num9 = 1;
                do
                {
                    num9 = (3 * num9) + 1;
                }
                while (num9 <= 0x100);
                do
                {
                    num9 /= 3;
                    num = num9;
                    while (num <= 0xff)
                    {
                        int num8 = numArray[num];
                        num2 = num;
                        while ((this.ftab[(numArray[num2 - num9] + 1) << 8] - this.ftab[numArray[num2 - num9] << 8]) > (this.ftab[(num8 + 1) << 8] - this.ftab[num8 << 8]))
                        {
                            numArray[num2] = numArray[num2 - num9];
                            num2 -= num9;
                            if (num2 <= (num9 - 1))
                            {
                                break;
                            }
                        }
                        numArray[num2] = num8;
                        num++;
                    }
                }
                while (num9 != 1);
                for (num = 0; num <= 0xff; num++)
                {
                    int num3 = numArray[num];
                    num2 = 0;
                    while (num2 <= 0xff)
                    {
                        int num4 = (num3 << 8) + num2;
                        if ((this.ftab[num4] & 0x200000) != 0x200000)
                        {
                            int loSt = this.ftab[num4] & -2097153;
                            int hiSt = (this.ftab[num4 + 1] & -2097153) - 1;
                            if (hiSt > loSt)
                            {
                                this.QSort3(loSt, hiSt, 2);
                                num7 += (hiSt - loSt) + 1;
                                if ((this.workDone > this.workLimit) && this.firstAttempt)
                                {
                                    break;
                                }
                            }
                            this.ftab[num4] |= 0x200000;
                        }
                        num2++;
                    }
                    flagArray[num3] = true;
                    if (num < 0xff)
                    {
                        int num12 = this.ftab[num3 << 8] & -2097153;
                        int num13 = (this.ftab[(num3 + 1) << 8] & -2097153) - num12;
                        int num14 = 0;
                        while ((num13 >> num14) > 0xfffe)
                        {
                            num14++;
                        }
                        num2 = 0;
                        while (num2 < num13)
                        {
                            int num15 = this.zptr[num12 + num2];
                            int num16 = num2 >> num14;
                            this.quadrant[num15] = num16;
                            if (num15 < 20)
                            {
                                this.quadrant[(num15 + this.last) + 1] = num16;
                            }
                            num2++;
                        }
                        if (((num13 - 1) >> num14) > 0xffff)
                        {
                            Panic();
                        }
                    }
                    num2 = 0;
                    while (num2 <= 0xff)
                    {
                        numArray2[num2] = this.ftab[(num2 << 8) + num3] & -2097153;
                        num2++;
                    }
                    num2 = this.ftab[num3 << 8] & -2097153;
                    while (num2 < (this.ftab[(num3 + 1) << 8] & -2097153))
                    {
                        index = this.block[this.zptr[num2]];
                        if (!flagArray[index])
                        {
                            this.zptr[numArray2[index]] = (this.zptr[num2] == 0) ? this.last : (this.zptr[num2] - 1);
                            numArray2[index]++;
                        }
                        num2++;
                    }
                    for (num2 = 0; num2 <= 0xff; num2++)
                    {
                        this.ftab[(num2 << 8) + num3] |= 0x200000;
                    }
                }
            }
        }

        private void MakeMaps()
        {
            this.nInUse = 0;
            for (int i = 0; i < 0x100; i++)
            {
                if (this.inUse[i])
                {
                    this.seqToUnseq[this.nInUse] = (char) i;
                    this.unseqToSeq[i] = (char) this.nInUse;
                    this.nInUse++;
                }
            }
        }

        private char Med3(char a, char b, char c)
        {
            char ch;
            if (a > b)
            {
                ch = a;
                a = b;
                b = ch;
            }
            if (b > c)
            {
                ch = b;
                b = c;
                c = ch;
            }
            if (a > b)
            {
                b = a;
            }
            return b;
        }

        private void MoveToFrontCodeAndSend()
        {
            this.BsPutIntVS(0x18, this.origPtr);
            this.GenerateMTFValues();
            this.SendMTFValues();
        }

        private static void Panic()
        {
        }

        private void QSort3(int loSt, int hiSt, int dSt)
        {
            StackElem[] elemArray = new StackElem[0x3e8];
            for (int i = 0; i < 0x3e8; i++)
            {
                elemArray[i] = new StackElem();
            }
            int index = 0;
            elemArray[index].ll = loSt;
            elemArray[index].hh = hiSt;
            elemArray[index].dd = dSt;
            index++;
            while (index > 0)
            {
                int num3;
                int num4;
                int num13;
                bool flag;
                if (index >= 0x3e8)
                {
                    Panic();
                }
                index--;
                int ll = elemArray[index].ll;
                int hh = elemArray[index].hh;
                int dd = elemArray[index].dd;
                if (((hh - ll) < 20) || (dd > 10))
                {
                    this.SimpleSort(ll, hh, dd);
                    if ((this.workDone <= this.workLimit) || !this.firstAttempt)
                    {
                        continue;
                    }
                    break;
                }
                int num5 = this.Med3(this.block[(this.zptr[ll] + dd) + 1], this.block[(this.zptr[hh] + dd) + 1], this.block[(this.zptr[(ll + hh) >> 1] + dd) + 1]);
                int num = num3 = ll;
                int num2 = num4 = hh;
                goto Label_02AD;
            Label_015B:
                if (num > num2)
                {
                    goto Label_0268;
                }
                int n = this.block[(this.zptr[num] + dd) + 1] - num5;
                if (n == 0)
                {
                    num13 = 0;
                    num13 = this.zptr[num];
                    this.zptr[num] = this.zptr[num3];
                    this.zptr[num3] = num13;
                    num3++;
                    num++;
                }
                else
                {
                    if (n > 0)
                    {
                        goto Label_0268;
                    }
                    num++;
                }
            Label_01DB:
                flag = true;
                goto Label_015B;
            Label_01E8:
                if (num > num2)
                {
                    goto Label_0270;
                }
                n = this.block[(this.zptr[num2] + dd) + 1] - num5;
                if (n == 0)
                {
                    num13 = 0;
                    num13 = this.zptr[num2];
                    this.zptr[num2] = this.zptr[num4];
                    this.zptr[num4] = num13;
                    num4--;
                    num2--;
                }
                else
                {
                    if (n < 0)
                    {
                        goto Label_0270;
                    }
                    num2--;
                }
            Label_0268:
                flag = true;
                goto Label_01E8;
            Label_0270:
                if (num > num2)
                {
                    goto Label_02B5;
                }
                int num14 = this.zptr[num];
                this.zptr[num] = this.zptr[num2];
                this.zptr[num2] = num14;
                num++;
                num2--;
            Label_02AD:
                flag = true;
                goto Label_01DB;
            Label_02B5:
                if (num4 < num3)
                {
                    elemArray[index].ll = ll;
                    elemArray[index].hh = hh;
                    elemArray[index].dd = dd + 1;
                    index++;
                }
                else
                {
                    n = ((num3 - ll) < (num - num3)) ? (num3 - ll) : (num - num3);
                    this.Vswap(ll, num - n, n);
                    int num7 = ((hh - num4) < (num4 - num2)) ? (hh - num4) : (num4 - num2);
                    this.Vswap(num, (hh - num7) + 1, num7);
                    n = ((ll + num) - num3) - 1;
                    num7 = (hh - (num4 - num2)) + 1;
                    elemArray[index].ll = ll;
                    elemArray[index].hh = n;
                    elemArray[index].dd = dd;
                    index++;
                    elemArray[index].ll = n + 1;
                    elemArray[index].hh = num7 - 1;
                    elemArray[index].dd = dd + 1;
                    index++;
                    elemArray[index].ll = num7;
                    elemArray[index].hh = hh;
                    elemArray[index].dd = dd;
                    index++;
                }
            }
        }

        private void RandomiseBlock()
        {
            int num;
            int num2 = 0;
            int index = 0;
            for (num = 0; num < 0x100; num++)
            {
                this.inUse[num] = false;
            }
            for (num = 0; num <= this.last; num++)
            {
                if (num2 == 0)
                {
                    num2 = (ushort) BZip2Constants.rNums[index];
                    index++;
                    if (index == 0x200)
                    {
                        index = 0;
                    }
                }
                num2--;
                this.block[num + 1] = (char) (this.block[num + 1] ^ ((num2 == 1) ? '\x0001' : '\0'));
                this.block[num + 1] = (char) (this.block[num + 1] & '\x00ff');
                this.inUse[this.block[num + 1]] = true;
            }
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            return 0;
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            return 0L;
        }

        private void SendMTFValues()
        {
            int num;
            int num3;
            int num4;
            int num6;
            int num16;
            bool flag;
            char[][] chArray = CBZip2InputStream.InitCharArray(6, 0x102);
            int index = 0;
            int alphaSize = this.nInUse + 2;
            int num2 = 0;
            while (num2 < 6)
            {
                num = 0;
                while (num < alphaSize)
                {
                    chArray[num2][num] = '\x000f';
                    num++;
                }
                num2++;
            }
            if (this.nMTF <= 0)
            {
                Panic();
            }
            if (this.nMTF < 200)
            {
                num16 = 2;
            }
            else if (this.nMTF < 600)
            {
                num16 = 3;
            }
            else if (this.nMTF < 0x4b0)
            {
                num16 = 4;
            }
            else if (this.nMTF < 0x960)
            {
                num16 = 5;
            }
            else
            {
                num16 = 6;
            }
            int num17 = num16;
            int nMTF = this.nMTF;
            int num5 = 0;
            while (num17 > 0)
            {
                int num19 = nMTF / num17;
                num6 = num5 - 1;
                int num20 = 0;
                while ((num20 < num19) && (num6 < (alphaSize - 1)))
                {
                    num6++;
                    num20 += this.mtfFreq[num6];
                }
                if ((((num6 > num5) && (num17 != num16)) && (num17 != 1)) && (((num16 - num17) % 2) == 1))
                {
                    num20 -= this.mtfFreq[num6];
                    num6--;
                }
                num = 0;
                while (num < alphaSize)
                {
                    if ((num >= num5) && (num <= num6))
                    {
                        chArray[num17 - 1][num] = '\0';
                    }
                    else
                    {
                        chArray[num17 - 1][num] = '\x000f';
                    }
                    num++;
                }
                num17--;
                num5 = num6 + 1;
                nMTF -= num20;
            }
            int[][] numArray = CBZip2InputStream.InitIntArray(6, 0x102);
            int[] numArray2 = new int[6];
            short[] numArray3 = new short[6];
            for (int i = 0; i < 4; i++)
            {
                short num27;
                num2 = 0;
                while (num2 < num16)
                {
                    numArray2[num2] = 0;
                    num2++;
                }
                num2 = 0;
                while (num2 < num16)
                {
                    for (num = 0; num < alphaSize; num++)
                    {
                        numArray[num2][num] = 0;
                    }
                    num2++;
                }
                index = 0;
                int num7 = 0;
                num5 = 0;
                goto Label_0463;
            Label_0252:
                if (num5 >= this.nMTF)
                {
                    goto Label_046B;
                }
                num6 = (num5 + 50) - 1;
                if (num6 >= this.nMTF)
                {
                    num6 = this.nMTF - 1;
                }
                num2 = 0;
                while (num2 < num16)
                {
                    numArray3[num2] = 0;
                    num2++;
                }
                if (num16 == 6)
                {
                    short num22;
                    short num23;
                    short num24;
                    short num25;
                    short num26;
                    short num21 = num22 = num23 = num24 = num25 = (short) (num26 = 0);
                    num3 = num5;
                    while (num3 <= num6)
                    {
                        num27 = this.szptr[num3];
                        num21 = (short) (num21 + ((short) chArray[0][num27]));
                        num22 = (short) (num22 + ((short) chArray[1][num27]));
                        num23 = (short) (num23 + ((short) chArray[2][num27]));
                        num24 = (short) (num24 + ((short) chArray[3][num27]));
                        num25 = (short) (num25 + ((short) chArray[4][num27]));
                        num26 = (short) (num26 + ((short) chArray[5][num27]));
                        num3++;
                    }
                    numArray3[0] = num21;
                    numArray3[1] = num22;
                    numArray3[2] = num23;
                    numArray3[3] = num24;
                    numArray3[4] = num25;
                    numArray3[5] = num26;
                }
                else
                {
                    num3 = num5;
                    while (num3 <= num6)
                    {
                        num27 = this.szptr[num3];
                        num2 = 0;
                        while (num2 < num16)
                        {
                            numArray3[num2] = (short) (numArray3[num2] + ((short) chArray[num2][num27]));
                            num2++;
                        }
                        num3++;
                    }
                }
                int num9 = 0x3b9ac9ff;
                int num8 = -1;
                num2 = 0;
                while (num2 < num16)
                {
                    if (numArray3[num2] < num9)
                    {
                        num9 = numArray3[num2];
                        num8 = num2;
                    }
                    num2++;
                }
                num7 += num9;
                numArray2[num8]++;
                this.selector[index] = (char) num8;
                index++;
                num3 = num5;
                while (num3 <= num6)
                {
                    numArray[num8][this.szptr[num3]]++;
                    num3++;
                }
                num5 = num6 + 1;
            Label_0463:
                flag = true;
                goto Label_0252;
            Label_046B:
                num2 = 0;
                while (num2 < num16)
                {
                    HbMakeCodeLengths(chArray[num2], numArray[num2], alphaSize, 20);
                    num2++;
                }
            }
            numArray = null;
            numArray2 = null;
            numArray3 = null;
            if (num16 >= 8)
            {
                Panic();
            }
            if ((index >= 0x8000) || (index > 0x4652))
            {
                Panic();
            }
            char[] chArray2 = new char[6];
            for (num3 = 0; num3 < num16; num3++)
            {
                chArray2[num3] = (char) num3;
            }
            num3 = 0;
            while (num3 < index)
            {
                char ch = this.selector[num3];
                num4 = 0;
                char ch3 = chArray2[num4];
                while (ch != ch3)
                {
                    num4++;
                    char ch2 = ch3;
                    ch3 = chArray2[num4];
                    chArray2[num4] = ch2;
                }
                chArray2[0] = ch3;
                this.selectorMtf[num3] = (char) num4;
                num3++;
            }
            int[][] numArray4 = CBZip2InputStream.InitIntArray(6, 0x102);
            for (num2 = 0; num2 < num16; num2++)
            {
                int minLen = 0x20;
                int maxLen = 0;
                num3 = 0;
                while (num3 < alphaSize)
                {
                    if (chArray[num2][num3] > maxLen)
                    {
                        maxLen = chArray[num2][num3];
                    }
                    if (chArray[num2][num3] < minLen)
                    {
                        minLen = chArray[num2][num3];
                    }
                    num3++;
                }
                if (maxLen > 20)
                {
                    Panic();
                }
                if (minLen < 1)
                {
                    Panic();
                }
                this.HbAssignCodes(numArray4[num2], chArray[num2], minLen, maxLen, alphaSize);
            }
            bool[] flagArray = new bool[0x10];
            for (num3 = 0; num3 < 0x10; num3++)
            {
                flagArray[num3] = false;
                num4 = 0;
                while (num4 < 0x10)
                {
                    if (this.inUse[(num3 * 0x10) + num4])
                    {
                        flagArray[num3] = true;
                    }
                    num4++;
                }
            }
            for (num3 = 0; num3 < 0x10; num3++)
            {
                if (flagArray[num3])
                {
                    this.BsW(1, 1);
                }
                else
                {
                    this.BsW(1, 0);
                }
            }
            for (num3 = 0; num3 < 0x10; num3++)
            {
                if (flagArray[num3])
                {
                    num4 = 0;
                    while (num4 < 0x10)
                    {
                        if (this.inUse[(num3 * 0x10) + num4])
                        {
                            this.BsW(1, 1);
                        }
                        else
                        {
                            this.BsW(1, 0);
                        }
                        num4++;
                    }
                }
            }
            this.BsW(3, num16);
            this.BsW(15, index);
            num3 = 0;
            while (num3 < index)
            {
                for (num4 = 0; num4 < this.selectorMtf[num3]; num4++)
                {
                    this.BsW(1, 1);
                }
                this.BsW(1, 0);
                num3++;
            }
            for (num2 = 0; num2 < num16; num2++)
            {
                int v = chArray[num2][0];
                this.BsW(5, v);
                num3 = 0;
                while (num3 < alphaSize)
                {
                    while (v < chArray[num2][num3])
                    {
                        this.BsW(2, 2);
                        v++;
                    }
                    while (v > chArray[num2][num3])
                    {
                        this.BsW(2, 3);
                        v--;
                    }
                    this.BsW(1, 0);
                    num3++;
                }
            }
            int num15 = 0;
            num5 = 0;
            while (true)
            {
                flag = true;
                if (num5 >= this.nMTF)
                {
                    if (num15 != index)
                    {
                        Panic();
                    }
                    return;
                }
                num6 = (num5 + 50) - 1;
                if (num6 >= this.nMTF)
                {
                    num6 = this.nMTF - 1;
                }
                for (num3 = num5; num3 <= num6; num3++)
                {
                    this.BsW(chArray[this.selector[num15]][this.szptr[num3]], numArray4[this.selector[num15]][this.szptr[num3]]);
                }
                num5 = num6 + 1;
                num15++;
            }
        }

        public override void SetLength(long value)
        {
        }

        private void SimpleSort(int lo, int hi, int d)
        {
            int num4 = (hi - lo) + 1;
            if (num4 >= 2)
            {
                int index = 0;
                while (this.incs[index] < num4)
                {
                    index++;
                }
                index--;
                while (index >= 0)
                {
                    bool flag;
                    int num3 = this.incs[index];
                    int num = lo + num3;
                    goto Label_01DA;
                Label_0058:
                    if (num > hi)
                    {
                        goto Label_01E2;
                    }
                    int num6 = this.zptr[num];
                    int num2 = num;
                    while (this.FullGtU(this.zptr[num2 - num3] + d, num6 + d))
                    {
                        this.zptr[num2] = this.zptr[num2 - num3];
                        num2 -= num3;
                        if (num2 <= ((lo + num3) - 1))
                        {
                            break;
                        }
                    }
                    this.zptr[num2] = num6;
                    num++;
                    if (num > hi)
                    {
                        goto Label_01E2;
                    }
                    num6 = this.zptr[num];
                    num2 = num;
                    while (this.FullGtU(this.zptr[num2 - num3] + d, num6 + d))
                    {
                        this.zptr[num2] = this.zptr[num2 - num3];
                        num2 -= num3;
                        if (num2 <= ((lo + num3) - 1))
                        {
                            break;
                        }
                    }
                    this.zptr[num2] = num6;
                    num++;
                    if (num > hi)
                    {
                        goto Label_01E2;
                    }
                    num6 = this.zptr[num];
                    num2 = num;
                    while (this.FullGtU(this.zptr[num2 - num3] + d, num6 + d))
                    {
                        this.zptr[num2] = this.zptr[num2 - num3];
                        num2 -= num3;
                        if (num2 <= ((lo + num3) - 1))
                        {
                            break;
                        }
                    }
                    this.zptr[num2] = num6;
                    num++;
                    if ((this.workDone > this.workLimit) && this.firstAttempt)
                    {
                        break;
                    }
                Label_01DA:
                    flag = true;
                    goto Label_0058;
                Label_01E2:
                    index--;
                }
            }
        }

        private void Vswap(int p1, int p2, int n)
        {
            int num = 0;
            while (n > 0)
            {
                num = this.zptr[p1];
                this.zptr[p1] = this.zptr[p2];
                this.zptr[p2] = num;
                p1++;
                p2++;
                n--;
            }
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            for (int i = 0; i < count; i++)
            {
                this.WriteByte(buffer[i + offset]);
            }
        }

        public override void WriteByte(byte bv)
        {
            int num = (0x100 + bv) % 0x100;
            if (this.currentChar != -1)
            {
                if (this.currentChar == num)
                {
                    this.runLength++;
                    if (this.runLength > 0xfe)
                    {
                        this.WriteRun();
                        this.currentChar = -1;
                        this.runLength = 0;
                    }
                }
                else
                {
                    this.WriteRun();
                    this.runLength = 1;
                    this.currentChar = num;
                }
            }
            else
            {
                this.currentChar = num;
                this.runLength++;
            }
        }

        private void WriteRun()
        {
            if (this.last < this.allowableBlockSize)
            {
                this.inUse[this.currentChar] = true;
                for (int i = 0; i < this.runLength; i++)
                {
                    this.mCrc.UpdateCRC((ushort) this.currentChar);
                }
                switch (this.runLength)
                {
                    case 1:
                        this.last++;
                        this.block[this.last + 1] = (char) this.currentChar;
                        return;

                    case 2:
                        this.last++;
                        this.block[this.last + 1] = (char) this.currentChar;
                        this.last++;
                        this.block[this.last + 1] = (char) this.currentChar;
                        return;

                    case 3:
                        this.last++;
                        this.block[this.last + 1] = (char) this.currentChar;
                        this.last++;
                        this.block[this.last + 1] = (char) this.currentChar;
                        this.last++;
                        this.block[this.last + 1] = (char) this.currentChar;
                        return;
                }
                this.inUse[this.runLength - 4] = true;
                this.last++;
                this.block[this.last + 1] = (char) this.currentChar;
                this.last++;
                this.block[this.last + 1] = (char) this.currentChar;
                this.last++;
                this.block[this.last + 1] = (char) this.currentChar;
                this.last++;
                this.block[this.last + 1] = (char) this.currentChar;
                this.last++;
                this.block[this.last + 1] = (char) (this.runLength - 4);
            }
            else
            {
                this.EndBlock();
                this.InitBlock();
                this.WriteRun();
            }
        }

        public override bool CanRead
        {
            get
            {
                return false;
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
                return true;
            }
        }

        public override long Length
        {
            get
            {
                return 0L;
            }
        }

        public override long Position
        {
            get
            {
                return 0L;
            }
            set
            {
            }
        }

        internal class StackElem
        {
            internal int dd;
            internal int hh;
            internal int ll;
        }
    }
}

