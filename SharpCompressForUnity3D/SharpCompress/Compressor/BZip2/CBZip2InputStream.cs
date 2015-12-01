namespace SharpCompress.Compressor.BZip2
{
    using System;
    using System.IO;

    internal class CBZip2InputStream : Stream
    {
        private int[][] basev = InitIntArray(6, 0x102);
        private bool blockRandomised;
        private int blockSize100k;
        private int bsBuff;
        private int bsLive;
        private Stream bsStream;
        private int ch2;
        private int chPrev;
        private int computedBlockCRC;
        private int computedCombinedCRC;
        private int count;
        private int currentChar = -1;
        private int currentState = 1;
        private bool decompressConcatenated;
        private int i;
        private int i2;
        private bool[] inUse = new bool[0x100];
        private bool isDisposed;
        private int j2;
        private int last;
        private bool leaveOpen;
        private int[][] limit = InitIntArray(6, 0x102);
        private char[] ll8;
        private CRC mCrc = new CRC();
        private int[] minLens = new int[6];
        private int nInUse;
        private const int NO_RAND_PART_A_STATE = 5;
        private const int NO_RAND_PART_B_STATE = 6;
        private const int NO_RAND_PART_C_STATE = 7;
        private int origPtr;
        private int[][] perm = InitIntArray(6, 0x102);
        private const int RAND_PART_A_STATE = 2;
        private const int RAND_PART_B_STATE = 3;
        private const int RAND_PART_C_STATE = 4;
        private int rNToGo = 0;
        private int rTPos = 0;
        private char[] selector = new char[0x4652];
        private char[] selectorMtf = new char[0x4652];
        private char[] seqToUnseq = new char[0x100];
        private const int START_BLOCK_STATE = 1;
        private int storedBlockCRC;
        private int storedCombinedCRC;
        private bool streamEnd = false;
        private int tPos;
        private int[] tt;
        private char[] unseqToSeq = new char[0x100];
        private int[] unzftab = new int[0x100];
        private char z;

        public CBZip2InputStream(Stream zStream, bool decompressConcatenated, bool leaveOpen)
        {
            this.decompressConcatenated = decompressConcatenated;
            this.ll8 = null;
            this.tt = null;
            this.BsSetStream(zStream, leaveOpen);
            this.Initialize(true);
            this.InitBlock();
            this.SetupBlock();
        }

        private static void BadBGLengths()
        {
            Cadvise();
        }

        private static void BadBlockHeader()
        {
            Cadvise();
        }

        private static void BitStreamEOF()
        {
            Cadvise();
        }

        private static void BlockOverrun()
        {
            Cadvise();
        }

        private void BsFinishedWithStream()
        {
            try
            {
                if (this.bsStream != null)
                {
                    if (!this.leaveOpen)
                    {
                        this.bsStream.Dispose();
                    }
                    this.bsStream = null;
                }
            }
            catch
            {
            }
        }

        private int BsGetint()
        {
            int num = 0;
            num = (num << 8) | this.BsR(8);
            num = (num << 8) | this.BsR(8);
            num = (num << 8) | this.BsR(8);
            return ((num << 8) | this.BsR(8));
        }

        private int BsGetInt32()
        {
            return this.BsGetint();
        }

        private int BsGetIntVS(int numBits)
        {
            return this.BsR(numBits);
        }

        private char BsGetUChar()
        {
            return (char) this.BsR(8);
        }

        private int BsR(int n)
        {
            while (this.bsLive < n)
            {
                int num3 = 0;
                try
                {
                    num3 = (ushort) this.bsStream.ReadByte();
                }
                catch (IOException)
                {
                    CompressedStreamEOF();
                }
                if (num3 == 0xffff)
                {
                    CompressedStreamEOF();
                }
                int num2 = num3;
                this.bsBuff = (this.bsBuff << 8) | (num2 & 0xff);
                this.bsLive += 8;
            }
            int num = (this.bsBuff >> (this.bsLive - n)) & ((((int) 1) << n) - 1);
            this.bsLive -= n;
            return num;
        }

        private void BsSetStream(Stream f, bool leaveOpen)
        {
            this.bsStream = f;
            this.bsLive = 0;
            this.bsBuff = 0;
            this.leaveOpen = leaveOpen;
        }

        private static void Cadvise()
        {
        }

        private bool Complete()
        {
            this.storedCombinedCRC = this.BsGetInt32();
            if (this.storedCombinedCRC != this.computedCombinedCRC)
            {
                CrcError();
            }
            bool flag = !this.decompressConcatenated || !this.Initialize(false);
            if (flag)
            {
                this.BsFinishedWithStream();
                this.streamEnd = true;
            }
            return flag;
        }

        private static void CompressedStreamEOF()
        {
            Cadvise();
        }

        private static void CrcError()
        {
            Cadvise();
        }

        protected override void Dispose(bool disposing)
        {
            if (!this.isDisposed)
            {
                this.isDisposed = true;
                base.Dispose(disposing);
                if (this.bsStream != null)
                {
                    this.bsStream.Dispose();
                }
            }
        }

        private void EndBlock()
        {
            this.computedBlockCRC = this.mCrc.GetFinalCRC();
            if (this.storedBlockCRC != this.computedBlockCRC)
            {
                CrcError();
            }
            this.computedCombinedCRC = (this.computedCombinedCRC << 1) | (this.computedCombinedCRC >> 0x1f);
            this.computedCombinedCRC ^= this.computedBlockCRC;
        }

        public override void Flush()
        {
        }

        private void GetAndMoveToFrontDecode()
        {
            int num;
            int num11;
            int num12;
            char ch;
            bool flag;
            char[] chArray = new char[0x100];
            int num4 = 0x186a0 * this.blockSize100k;
            this.origPtr = this.BsGetIntVS(0x18);
            this.RecvDecodingTables();
            int num5 = this.nInUse + 1;
            int index = -1;
            int num7 = 0;
            for (num = 0; num <= 0xff; num++)
            {
                this.unzftab[num] = 0;
            }
            for (num = 0; num <= 0xff; num++)
            {
                chArray[num] = (char) num;
            }
            this.last = -1;
            if (num7 == 0)
            {
                index++;
                num7 = 50;
            }
            num7--;
            int num8 = this.selector[index];
            int n = this.minLens[num8];
            int num10 = this.BsR(n);
            while (num10 > this.limit[num8][n])
            {
                n++;
                while (this.bsLive < 1)
                {
                    ch = '\0';
                    try
                    {
                        ch = (char) this.bsStream.ReadByte();
                    }
                    catch (IOException)
                    {
                        CompressedStreamEOF();
                    }
                    if (ch == 0xffff)
                    {
                        CompressedStreamEOF();
                    }
                    num12 = ch;
                    this.bsBuff = (this.bsBuff << 8) | (num12 & 0xff);
                    this.bsLive += 8;
                }
                num11 = (this.bsBuff >> (this.bsLive - 1)) & 1;
                this.bsLive--;
                num10 = (num10 << 1) | num11;
            }
            int num3 = this.perm[num8][num10 - this.basev[num8][n]];
        Label_05B0:
            flag = true;
            if (num3 == num5)
            {
                return;
            }
            switch (num3)
            {
                case 0:
                case 1:
                {
                    int num13 = -1;
                    int num14 = 1;
                    do
                    {
                        switch (num3)
                        {
                            case 0:
                                num13 += num14;
                                break;

                            case 1:
                                num13 += 2 * num14;
                                break;
                        }
                        num14 *= 2;
                        if (num7 == 0)
                        {
                            index++;
                            num7 = 50;
                        }
                        num7--;
                        num8 = this.selector[index];
                        n = this.minLens[num8];
                        num10 = this.BsR(n);
                        while (num10 > this.limit[num8][n])
                        {
                            n++;
                            while (this.bsLive < 1)
                            {
                                ch = '\0';
                                try
                                {
                                    ch = (char) this.bsStream.ReadByte();
                                }
                                catch (IOException)
                                {
                                    CompressedStreamEOF();
                                }
                                if (ch == 0xffff)
                                {
                                    CompressedStreamEOF();
                                }
                                num12 = ch;
                                this.bsBuff = (this.bsBuff << 8) | (num12 & 0xff);
                                this.bsLive += 8;
                            }
                            num11 = (this.bsBuff >> (this.bsLive - 1)) & 1;
                            this.bsLive--;
                            num10 = (num10 << 1) | num11;
                        }
                        num3 = this.perm[num8][num10 - this.basev[num8][n]];
                    }
                    while ((num3 == 0) || (num3 == 1));
                    num13++;
                    char ch2 = this.seqToUnseq[chArray[0]];
                    this.unzftab[ch2] += num13;
                    while (num13 > 0)
                    {
                        this.last++;
                        this.ll8[this.last] = ch2;
                        num13--;
                    }
                    if (this.last >= num4)
                    {
                        BlockOverrun();
                    }
                    goto Label_05B0;
                }
            }
            this.last++;
            if (this.last >= num4)
            {
                BlockOverrun();
            }
            char ch3 = chArray[num3 - 1];
            this.unzftab[this.seqToUnseq[ch3]]++;
            this.ll8[this.last] = this.seqToUnseq[ch3];
            int num2 = num3 - 1;
            while (num2 > 3)
            {
                chArray[num2] = chArray[num2 - 1];
                chArray[num2 - 1] = chArray[num2 - 2];
                chArray[num2 - 2] = chArray[num2 - 3];
                chArray[num2 - 3] = chArray[num2 - 4];
                num2 -= 4;
            }
            while (num2 > 0)
            {
                chArray[num2] = chArray[num2 - 1];
                num2--;
            }
            chArray[0] = ch3;
            if (num7 == 0)
            {
                index++;
                num7 = 50;
            }
            num7--;
            num8 = this.selector[index];
            n = this.minLens[num8];
            num10 = this.BsR(n);
            while (num10 > this.limit[num8][n])
            {
                n++;
                while (this.bsLive < 1)
                {
                    ch = '\0';
                    try
                    {
                        ch = (char) this.bsStream.ReadByte();
                    }
                    catch (IOException)
                    {
                        CompressedStreamEOF();
                    }
                    num12 = ch;
                    this.bsBuff = (this.bsBuff << 8) | (num12 & 0xff);
                    this.bsLive += 8;
                }
                num11 = (this.bsBuff >> (this.bsLive - 1)) & 1;
                this.bsLive--;
                num10 = (num10 << 1) | num11;
            }
            num3 = this.perm[num8][num10 - this.basev[num8][n]];
            goto Label_05B0;
        }

        private void HbCreateDecodeTables(int[] limit, int[] basev, int[] perm, char[] length, int minLen, int maxLen, int alphaSize)
        {
            int num2;
            int index = 0;
            for (num2 = minLen; num2 <= maxLen; num2++)
            {
                for (int i = 0; i < alphaSize; i++)
                {
                    if (length[i] == num2)
                    {
                        perm[index] = i;
                        index++;
                    }
                }
            }
            for (num2 = 0; num2 < 0x17; num2++)
            {
                basev[num2] = 0;
            }
            for (num2 = 0; num2 < alphaSize; num2++)
            {
                basev[length[num2] + '\x0001']++;
            }
            for (num2 = 1; num2 < 0x17; num2++)
            {
                basev[num2] += basev[num2 - 1];
            }
            for (num2 = 0; num2 < 0x17; num2++)
            {
                limit[num2] = 0;
            }
            int num4 = 0;
            for (num2 = minLen; num2 <= maxLen; num2++)
            {
                num4 += basev[num2 + 1] - basev[num2];
                limit[num2] = num4 - 1;
                num4 = num4 << 1;
            }
            for (num2 = minLen + 1; num2 <= maxLen; num2++)
            {
                basev[num2] = ((limit[num2 - 1] + 1) << 1) - basev[num2];
            }
        }

        private void InitBlock()
        {
            while (true)
            {
                char ch = this.BsGetUChar();
                char ch2 = this.BsGetUChar();
                char ch3 = this.BsGetUChar();
                char ch4 = this.BsGetUChar();
                char ch5 = this.BsGetUChar();
                char ch6 = this.BsGetUChar();
                if (((((ch != '\x0017') || (ch2 != 'r')) || ((ch3 != 'E') || (ch4 != '8'))) || (ch5 != 'P')) || (ch6 != '\x0090'))
                {
                    if (((((ch != '1') || (ch2 != 'A')) || ((ch3 != 'Y') || (ch4 != '&'))) || (ch5 != 'S')) || (ch6 != 'Y'))
                    {
                        BadBlockHeader();
                        this.streamEnd = true;
                    }
                    else
                    {
                        this.storedBlockCRC = this.BsGetInt32();
                        if (this.BsR(1) == 1)
                        {
                            this.blockRandomised = true;
                        }
                        else
                        {
                            this.blockRandomised = false;
                        }
                        this.GetAndMoveToFrontDecode();
                        this.mCrc.InitialiseCRC();
                        this.currentState = 1;
                    }
                    return;
                }
                if (this.Complete())
                {
                    return;
                }
            }
        }

        internal static char[][] InitCharArray(int n1, int n2)
        {
            char[][] chArray = new char[n1][];
            for (int i = 0; i < n1; i++)
            {
                chArray[i] = new char[n2];
            }
            return chArray;
        }

        private bool Initialize(bool isFirstStream)
        {
            int num = this.bsStream.ReadByte();
            int num2 = this.bsStream.ReadByte();
            int num3 = this.bsStream.ReadByte();
            if (!((num != -1) || isFirstStream))
            {
                return false;
            }
            if (((num != 0x42) || (num2 != 90)) || (num3 != 0x68))
            {
                throw new IOException("Not a BZIP2 marked stream");
            }
            int num4 = this.bsStream.ReadByte();
            if ((num4 < 0x31) || (num4 > 0x39))
            {
                this.BsFinishedWithStream();
                this.streamEnd = true;
                return false;
            }
            this.SetDecompressStructureSizes(num4 - 0x30);
            this.bsLive = 0;
            this.computedCombinedCRC = 0;
            return true;
        }

        internal static int[][] InitIntArray(int n1, int n2)
        {
            int[][] numArray = new int[n1][];
            for (int i = 0; i < n1; i++)
            {
                numArray[i] = new int[n2];
            }
            return numArray;
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

        public override int Read(byte[] buffer, int offset, int count)
        {
            int num = -1;
            int num2 = 0;
            while (num2 < count)
            {
                num = this.ReadByte();
                if (num == -1)
                {
                    return num2;
                }
                buffer[num2 + offset] = (byte) num;
                num2++;
            }
            return num2;
        }

        public override int ReadByte()
        {
            if (this.streamEnd)
            {
                return -1;
            }
            int currentChar = this.currentChar;
            switch (this.currentState)
            {
                case 1:
                case 2:
                case 5:
                    return currentChar;

                case 3:
                    this.SetupRandPartB();
                    return currentChar;

                case 4:
                    this.SetupRandPartC();
                    return currentChar;

                case 6:
                    this.SetupNoRandPartB();
                    return currentChar;

                case 7:
                    this.SetupNoRandPartC();
                    return currentChar;
            }
            return currentChar;
        }

        private void RecvDecodingTables()
        {
            int num;
            int num2;
            int num3;
            char[][] chArray = InitCharArray(6, 0x102);
            bool[] flagArray = new bool[0x10];
            for (num = 0; num < 0x10; num++)
            {
                if (this.BsR(1) == 1)
                {
                    flagArray[num] = true;
                }
                else
                {
                    flagArray[num] = false;
                }
            }
            for (num = 0; num < 0x100; num++)
            {
                this.inUse[num] = false;
            }
            for (num = 0; num < 0x10; num++)
            {
                if (flagArray[num])
                {
                    num2 = 0;
                    while (num2 < 0x10)
                    {
                        if (this.BsR(1) == 1)
                        {
                            this.inUse[(num * 0x10) + num2] = true;
                        }
                        num2++;
                    }
                }
            }
            this.MakeMaps();
            int alphaSize = this.nInUse + 2;
            int num4 = this.BsR(3);
            int num5 = this.BsR(15);
            for (num = 0; num < num5; num++)
            {
                num2 = 0;
                while (this.BsR(1) == 1)
                {
                    num2++;
                }
                this.selectorMtf[num] = (char) num2;
            }
            char[] chArray2 = new char[6];
            char index = '\0';
            while (index < num4)
            {
                chArray2[index] = index;
                index = (char) (index + '\x0001');
            }
            num = 0;
            while (num < num5)
            {
                index = this.selectorMtf[num];
                char ch = chArray2[index];
                while (index > '\0')
                {
                    chArray2[index] = chArray2[index - '\x0001'];
                    index = (char) (index - '\x0001');
                }
                chArray2[0] = ch;
                this.selector[num] = ch;
                num++;
            }
            for (num3 = 0; num3 < num4; num3++)
            {
                int num9 = this.BsR(5);
                num = 0;
                while (num < alphaSize)
                {
                    while (this.BsR(1) == 1)
                    {
                        if (this.BsR(1) == 0)
                        {
                            num9++;
                        }
                        else
                        {
                            num9--;
                        }
                    }
                    chArray[num3][num] = (char) num9;
                    num++;
                }
            }
            for (num3 = 0; num3 < num4; num3++)
            {
                int minLen = 0x20;
                int maxLen = 0;
                for (num = 0; num < alphaSize; num++)
                {
                    if (chArray[num3][num] > maxLen)
                    {
                        maxLen = chArray[num3][num];
                    }
                    if (chArray[num3][num] < minLen)
                    {
                        minLen = chArray[num3][num];
                    }
                }
                this.HbCreateDecodeTables(this.limit[num3], this.basev[num3], this.perm[num3], chArray[num3], minLen, maxLen, alphaSize);
                this.minLens[num3] = minLen;
            }
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            return 0L;
        }

        private void SetDecompressStructureSizes(int newSize100k)
        {
            if ((((0 > newSize100k) || (newSize100k > 9)) || (0 > this.blockSize100k)) || (this.blockSize100k > 9))
            {
            }
            this.blockSize100k = newSize100k;
            if (newSize100k != 0)
            {
                int num = 0x186a0 * newSize100k;
                this.ll8 = new char[num];
                this.tt = new int[num];
            }
        }

        public override void SetLength(long value)
        {
        }

        private void SetupBlock()
        {
            int[] numArray = new int[0x101];
            numArray[0] = 0;
            this.i = 1;
            while (this.i <= 0x100)
            {
                numArray[this.i] = this.unzftab[this.i - 1];
                this.i++;
            }
            this.i = 1;
            while (this.i <= 0x100)
            {
                numArray[this.i] += numArray[this.i - 1];
                this.i++;
            }
            this.i = 0;
            while (this.i <= this.last)
            {
                char index = this.ll8[this.i];
                this.tt[numArray[index]] = this.i;
                numArray[index]++;
                this.i++;
            }
            numArray = null;
            this.tPos = this.tt[this.origPtr];
            this.count = 0;
            this.i2 = 0;
            this.ch2 = 0x100;
            if (this.blockRandomised)
            {
                this.rNToGo = 0;
                this.rTPos = 0;
                this.SetupRandPartA();
            }
            else
            {
                this.SetupNoRandPartA();
            }
        }

        private void SetupNoRandPartA()
        {
            if (this.i2 <= this.last)
            {
                this.chPrev = this.ch2;
                this.ch2 = this.ll8[this.tPos];
                this.tPos = this.tt[this.tPos];
                this.i2++;
                this.currentChar = this.ch2;
                this.currentState = 6;
                this.mCrc.UpdateCRC(this.ch2);
            }
            else
            {
                this.EndBlock();
                this.InitBlock();
                this.SetupBlock();
            }
        }

        private void SetupNoRandPartB()
        {
            if (this.ch2 != this.chPrev)
            {
                this.currentState = 5;
                this.count = 1;
                this.SetupNoRandPartA();
            }
            else
            {
                this.count++;
                if (this.count >= 4)
                {
                    this.z = this.ll8[this.tPos];
                    this.tPos = this.tt[this.tPos];
                    this.currentState = 7;
                    this.j2 = 0;
                    this.SetupNoRandPartC();
                }
                else
                {
                    this.currentState = 5;
                    this.SetupNoRandPartA();
                }
            }
        }

        private void SetupNoRandPartC()
        {
            if (this.j2 < this.z)
            {
                this.currentChar = this.ch2;
                this.mCrc.UpdateCRC(this.ch2);
                this.j2++;
            }
            else
            {
                this.currentState = 5;
                this.i2++;
                this.count = 0;
                this.SetupNoRandPartA();
            }
        }

        private void SetupRandPartA()
        {
            if (this.i2 <= this.last)
            {
                this.chPrev = this.ch2;
                this.ch2 = this.ll8[this.tPos];
                this.tPos = this.tt[this.tPos];
                if (this.rNToGo == 0)
                {
                    this.rNToGo = BZip2Constants.rNums[this.rTPos];
                    this.rTPos++;
                    if (this.rTPos == 0x200)
                    {
                        this.rTPos = 0;
                    }
                }
                this.rNToGo--;
                this.ch2 ^= (this.rNToGo == 1) ? 1 : 0;
                this.i2++;
                this.currentChar = this.ch2;
                this.currentState = 3;
                this.mCrc.UpdateCRC(this.ch2);
            }
            else
            {
                this.EndBlock();
                this.InitBlock();
                this.SetupBlock();
            }
        }

        private void SetupRandPartB()
        {
            if (this.ch2 != this.chPrev)
            {
                this.currentState = 2;
                this.count = 1;
                this.SetupRandPartA();
            }
            else
            {
                this.count++;
                if (this.count >= 4)
                {
                    this.z = this.ll8[this.tPos];
                    this.tPos = this.tt[this.tPos];
                    if (this.rNToGo == 0)
                    {
                        this.rNToGo = BZip2Constants.rNums[this.rTPos];
                        this.rTPos++;
                        if (this.rTPos == 0x200)
                        {
                            this.rTPos = 0;
                        }
                    }
                    this.rNToGo--;
                    this.z = (char) (this.z ^ ((this.rNToGo == 1) ? '\x0001' : '\0'));
                    this.j2 = 0;
                    this.currentState = 4;
                    this.SetupRandPartC();
                }
                else
                {
                    this.currentState = 2;
                    this.SetupRandPartA();
                }
            }
        }

        private void SetupRandPartC()
        {
            if (this.j2 < this.z)
            {
                this.currentChar = this.ch2;
                this.mCrc.UpdateCRC(this.ch2);
                this.j2++;
            }
            else
            {
                this.currentState = 2;
                this.i2++;
                this.count = 0;
                this.SetupRandPartA();
            }
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
        }

        public override bool CanRead
        {
            get
            {
                return true;
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
                return false;
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
    }
}

