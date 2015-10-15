namespace SharpCompress.Compressor.Rar
{
    using SharpCompress;
    using SharpCompress.Common;
    using SharpCompress.Common.Rar.Headers;
    using SharpCompress.Compressor.PPMd.H;
    using SharpCompress.Compressor.Rar.decode;
    using SharpCompress.Compressor.Rar.PPM;
    using SharpCompress.Compressor.Rar.VM;
    using System;
    using System.Collections.Generic;
    using System.IO;

    internal sealed class Unpack : Unpack20
    {
        public static int[] DBitLengthCounts = new int[] { 
            4, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 
            14, 0, 12
         };
        private bool fileExtracted;
        private FileHeader fileHeader;
        private List<UnpackFilter> filters = new List<UnpackFilter>();
        private int lastFilter;
        private int lowDistRepCount;
        private List<int> oldFilterLengths = new List<int>();
        private ModelPPM ppm = new ModelPPM();
        private bool ppmError;
        private int ppmEscChar;
        private int prevLowDist;
        private List<UnpackFilter> prgStack = new List<UnpackFilter>();
        private RarVM rarVM = new RarVM();
        private bool tablesRead;
        private BlockTypes unpBlockType;
        private byte[] unpOldTable = new byte[Compress.HUFF_TABLE_SIZE];
        private long writtenFileSize;

        public Unpack()
        {
            base.window = null;
            base.suspended = false;
            base.unpAllBuf = false;
            base.unpSomeRead = false;
        }

        private bool addVMCode(int firstByte, List<byte> vmCode, int length)
        {
            int num;
            int lastFilter;
            UnpackFilter filter2;
            int num5;
            BitInput rarVM = new BitInput();
            rarVM.InitBitInput();
            for (num = 0; num < Math.Min(0x8000, vmCode.Count); num++)
            {
                rarVM.InBuf[num] = vmCode[num];
            }
            this.rarVM.init();
            if ((firstByte & 0x80) != 0)
            {
                lastFilter = RarVM.ReadData(rarVM);
                if (lastFilter == 0)
                {
                    this.initFilters();
                }
                else
                {
                    lastFilter--;
                }
            }
            else
            {
                lastFilter = this.lastFilter;
            }
            if ((lastFilter > this.filters.Count) || (lastFilter > this.oldFilterLengths.Count))
            {
                return false;
            }
            this.lastFilter = lastFilter;
            bool flag = lastFilter == this.filters.Count;
            UnpackFilter item = new UnpackFilter();
            if (flag)
            {
                if (lastFilter > 0x400)
                {
                    return false;
                }
                filter2 = new UnpackFilter();
                this.filters.Add(filter2);
                item.ParentFilter = this.filters.Count - 1;
                this.oldFilterLengths.Add(0);
                filter2.ExecCount = 0;
            }
            else
            {
                filter2 = this.filters[lastFilter];
                item.ParentFilter = lastFilter;
                filter2.ExecCount++;
            }
            this.prgStack.Add(item);
            item.ExecCount = filter2.ExecCount;
            int num3 = RarVM.ReadData(rarVM);
            if ((firstByte & 0x40) != 0)
            {
                num3 += 0x102;
            }
            item.BlockStart = (num3 + base.unpPtr) & Compress.MAXWINMASK;
            if ((firstByte & 0x20) != 0)
            {
                item.BlockLength = RarVM.ReadData(rarVM);
            }
            else
            {
                item.BlockLength = (lastFilter < this.oldFilterLengths.Count) ? this.oldFilterLengths[lastFilter] : 0;
            }
            item.NextWindow = (base.wrPtr != base.unpPtr) && (((base.wrPtr - base.unpPtr) & Compress.MAXWINMASK) <= num3);
            this.oldFilterLengths[lastFilter] = item.BlockLength;
            Utility.Fill<int>(item.Program.InitR, 0);
            item.Program.InitR[3] = 0x3c000;
            item.Program.InitR[4] = item.BlockLength;
            item.Program.InitR[5] = item.ExecCount;
            if ((firstByte & 0x10) != 0)
            {
                int num4 = Utility.URShift(rarVM.GetBits(), 9);
                rarVM.AddBits(7);
                for (num5 = 0; num5 < 7; num5++)
                {
                    if ((num4 & (((int) 1) << num5)) != 0)
                    {
                        item.Program.InitR[num5] = RarVM.ReadData(rarVM);
                    }
                }
            }
            if (flag)
            {
                int codeSize = RarVM.ReadData(rarVM);
                if ((codeSize >= 0x10000) || (codeSize == 0))
                {
                    return false;
                }
                byte[] code = new byte[codeSize];
                for (num5 = 0; num5 < codeSize; num5++)
                {
                    if (rarVM.Overflow(3))
                    {
                        return false;
                    }
                    code[num5] = (byte) (rarVM.GetBits() >> 8);
                    rarVM.AddBits(8);
                }
                this.rarVM.prepare(code, codeSize, filter2.Program);
            }
            item.Program.AltCommands = filter2.Program.Commands;
            item.Program.CommandCount = filter2.Program.CommandCount;
            int count = filter2.Program.StaticData.Count;
            if ((count > 0) && (count < 0x2000))
            {
                item.Program.StaticData = filter2.Program.StaticData;
            }
            if (item.Program.GlobalData.Count < 0x40)
            {
                item.Program.GlobalData.Clear();
                Utility.SetSize(item.Program.GlobalData, 0x40);
            }
            List<byte> globalData = item.Program.GlobalData;
            for (num5 = 0; num5 < 7; num5++)
            {
                this.rarVM.SetLowEndianValue(globalData, num5 * 4, item.Program.InitR[num5]);
            }
            this.rarVM.SetLowEndianValue(globalData, 0x1c, item.BlockLength);
            this.rarVM.SetLowEndianValue(globalData, 0x20, 0);
            this.rarVM.SetLowEndianValue(globalData, 0x24, 0);
            this.rarVM.SetLowEndianValue(globalData, 40, 0);
            this.rarVM.SetLowEndianValue(globalData, 0x2c, item.ExecCount);
            for (num = 0; num < 0x10; num++)
            {
                globalData[0x30 + num] = 0;
            }
            if ((firstByte & 8) != 0)
            {
                if (rarVM.Overflow(3))
                {
                    return false;
                }
                int num8 = RarVM.ReadData(rarVM);
                if (num8 > 0x1fc0)
                {
                    return false;
                }
                int num9 = item.Program.GlobalData.Count;
                if (num9 < (num8 + 0x40))
                {
                    Utility.SetSize(item.Program.GlobalData, (num8 + 0x40) - num9);
                }
                int num10 = 0x40;
                globalData = item.Program.GlobalData;
                for (num5 = 0; num5 < num8; num5++)
                {
                    if (rarVM.Overflow(3))
                    {
                        return false;
                    }
                    globalData[num10 + num5] = (byte) Utility.URShift(rarVM.GetBits(), 8);
                    rarVM.AddBits(8);
                }
            }
            return true;
        }

        public void cleanUp()
        {
            if (this.ppm != null)
            {
                SubAllocator subAlloc = this.ppm.SubAlloc;
                if (subAlloc != null)
                {
                    subAlloc.stopSubAllocator();
                }
            }
        }

        private void copyString(int length, int distance)
        {
            int num = base.unpPtr - distance;
            if (((num >= 0) && (num < 0x3ffefc)) && (base.unpPtr < 0x3ffefc))
            {
                base.window[base.unpPtr++] = base.window[num++];
                while (--length > 0)
                {
                    base.window[base.unpPtr++] = base.window[num++];
                }
            }
            else
            {
                while (length-- != 0)
                {
                    base.window[base.unpPtr] = base.window[num++ & Compress.MAXWINMASK];
                    base.unpPtr = (base.unpPtr + 1) & Compress.MAXWINMASK;
                }
            }
        }

        public void doUnpack()
        {
            bool solid = FlagUtility.HasFlag<FileFlags>(this.fileHeader.FileFlags, FileFlags.SOLID);
            if (this.fileHeader.PackingMethod == 0x30)
            {
                this.unstoreFile();
            }
            else
            {
                switch (this.fileHeader.RarVersion)
                {
                    case 0x1a:
                    case 20:
                        base.unpack20(solid);
                        break;

                    case 0x1d:
                    case 0x24:
                        this.unpack29(solid);
                        break;

                    case 15:
                        base.unpack15(solid);
                        break;
                }
            }
        }

        public void doUnpack(FileHeader fileHeader, Stream readStream, Stream writeStream)
        {
            base.destUnpSize = fileHeader.UncompressedSize;
            this.fileHeader = fileHeader;
            base.writeStream = writeStream;
            base.readStream = readStream;
            if (!FlagUtility.HasFlag<FileFlags>(fileHeader.FileFlags, FileFlags.SOLID))
            {
                this.init(null);
            }
            base.suspended = false;
            this.doUnpack();
        }

        private void ExecuteCode(VMPreparedProgram Prg)
        {
            if (Prg.GlobalData.Count > 0)
            {
                Prg.InitR[6] = (int) this.writtenFileSize;
                this.rarVM.SetLowEndianValue(Prg.GlobalData, 0x24, (int) this.writtenFileSize);
                this.rarVM.SetLowEndianValue(Prg.GlobalData, 40, (int) Utility.URShift(this.writtenFileSize, 0x20));
                this.rarVM.execute(Prg);
            }
        }

        public void init(byte[] window)
        {
            if (window == null)
            {
                base.window = new byte[0x400000];
            }
            else
            {
                base.window = window;
            }
            base.inAddr = 0;
            this.unpInitData(false);
        }

        private void initFilters()
        {
            this.oldFilterLengths.Clear();
            this.lastFilter = 0;
            this.filters.Clear();
            this.prgStack.Clear();
        }

        private void insertLastMatch(int length, int distance)
        {
            base.lastDist = distance;
            base.lastLength = length;
        }

        private void insertOldDist(int distance)
        {
            base.oldDist[3] = base.oldDist[2];
            base.oldDist[2] = base.oldDist[1];
            base.oldDist[1] = base.oldDist[0];
            base.oldDist[0] = distance;
        }

        private bool readEndOfBlock()
        {
            bool flag;
            int bits = base.GetBits();
            bool flag2 = false;
            if ((bits & 0x8000) != 0)
            {
                flag = true;
                base.AddBits(1);
            }
            else
            {
                flag2 = true;
                flag = (bits & 0x4000) != 0;
                base.AddBits(2);
            }
            this.tablesRead = !flag;
            return (!flag2 && (!flag || this.readTables()));
        }

        private bool readTables()
        {
            int num2;
            byte[] lenTab = new byte[20];
            byte[] buffer2 = new byte[Compress.HUFF_TABLE_SIZE];
            if ((base.inAddr > (base.readTop - 0x19)) && !base.unpReadBuf())
            {
                return false;
            }
            base.AddBits((8 - base.inBit) & 7);
            long num = base.GetBits() & -1;
            if ((num & 0x8000L) != 0L)
            {
                this.unpBlockType = BlockTypes.BLOCK_PPM;
                return this.ppm.decodeInit(this, this.ppmEscChar);
            }
            this.unpBlockType = BlockTypes.BLOCK_LZ;
            this.prevLowDist = 0;
            this.lowDistRepCount = 0;
            if ((num & 0x4000L) == 0L)
            {
                Utility.Fill<byte>(this.unpOldTable, 0);
            }
            base.AddBits(2);
            for (num2 = 0; num2 < 20; num2++)
            {
                int num3 = Utility.URShift(base.GetBits(), 12) & 0xff;
                base.AddBits(4);
                if (num3 == 15)
                {
                    int num4 = Utility.URShift(base.GetBits(), 12) & 0xff;
                    base.AddBits(4);
                    if (num4 == 0)
                    {
                        lenTab[num2] = 15;
                    }
                    else
                    {
                        num4 += 2;
                        while ((num4-- > 0) && (num2 < lenTab.Length))
                        {
                            lenTab[num2++] = 0;
                        }
                        num2--;
                    }
                }
                else
                {
                    lenTab[num2] = (byte) num3;
                }
            }
            UnpackUtility.makeDecodeTables(lenTab, 0, base.BD, 20);
            int num5 = Compress.HUFF_TABLE_SIZE;
            num2 = 0;
            while (num2 < num5)
            {
                if ((base.inAddr > (base.readTop - 5)) && !base.unpReadBuf())
                {
                    return false;
                }
                int num6 = UnpackUtility.decodeNumber(this, base.BD);
                if (num6 < 0x10)
                {
                    buffer2[num2] = (byte) ((num6 + this.unpOldTable[num2]) & 15);
                    num2++;
                }
                else
                {
                    int num7;
                    if (num6 < 0x12)
                    {
                        if (num6 == 0x10)
                        {
                            num7 = Utility.URShift(base.GetBits(), 13) + 3;
                            base.AddBits(3);
                        }
                        else
                        {
                            num7 = Utility.URShift(base.GetBits(), 9) + 11;
                            base.AddBits(7);
                        }
                        while ((num7-- > 0) && (num2 < num5))
                        {
                            buffer2[num2] = buffer2[num2 - 1];
                            num2++;
                        }
                    }
                    else
                    {
                        if (num6 == 0x12)
                        {
                            num7 = Utility.URShift(base.GetBits(), 13) + 3;
                            base.AddBits(3);
                        }
                        else
                        {
                            num7 = Utility.URShift(base.GetBits(), 9) + 11;
                            base.AddBits(7);
                        }
                        while ((num7-- > 0) && (num2 < num5))
                        {
                            buffer2[num2++] = 0;
                        }
                    }
                }
            }
            this.tablesRead = true;
            if (base.inAddr > base.readTop)
            {
                return false;
            }
            UnpackUtility.makeDecodeTables(buffer2, 0, base.LD, 0x12b);
            UnpackUtility.makeDecodeTables(buffer2, 0x12b, base.DD, 60);
            UnpackUtility.makeDecodeTables(buffer2, 0x167, base.LDD, 0x11);
            UnpackUtility.makeDecodeTables(buffer2, 0x178, base.RD, 0x1c);
            Buffer.BlockCopy(buffer2, 0, this.unpOldTable, 0, this.unpOldTable.Length);
            return true;
        }

        private bool readVMCode()
        {
            int firstByte = base.GetBits() >> 8;
            base.AddBits(8);
            int length = (firstByte & 7) + 1;
            if (length == 7)
            {
                length = (base.GetBits() >> 8) + 7;
                base.AddBits(8);
            }
            else if (length == 8)
            {
                length = base.GetBits();
                base.AddBits(0x10);
            }
            List<byte> vmCode = new List<byte>();
            for (int i = 0; i < length; i++)
            {
                if (((base.inAddr >= (base.readTop - 1)) && !base.unpReadBuf()) && (i < (length - 1)))
                {
                    return false;
                }
                vmCode.Add((byte) (base.GetBits() >> 8));
                base.AddBits(8);
            }
            return this.addVMCode(firstByte, vmCode, length);
        }

        private bool readVMCodePPM()
        {
            int num3;
            int firstByte = this.ppm.decodeChar();
            if (firstByte == -1)
            {
                return false;
            }
            int length = (firstByte & 7) + 1;
            switch (length)
            {
                case 7:
                    num3 = this.ppm.decodeChar();
                    if (num3 == -1)
                    {
                        return false;
                    }
                    length = num3 + 7;
                    break;

                case 8:
                {
                    num3 = this.ppm.decodeChar();
                    if (num3 == -1)
                    {
                        return false;
                    }
                    int num4 = this.ppm.decodeChar();
                    if (num4 == -1)
                    {
                        return false;
                    }
                    length = (num3 * 0x100) + num4;
                    break;
                }
            }
            List<byte> vmCode = new List<byte>();
            for (int i = 0; i < length; i++)
            {
                int num6 = this.ppm.decodeChar();
                if (num6 == -1)
                {
                    return false;
                }
                vmCode.Add((byte) num6);
            }
            return this.addVMCode(firstByte, vmCode, length);
        }

        private void unpack29(bool solid)
        {
            int num5;
            bool flag2;
            int[] numArray = new int[60];
            byte[] buffer = new byte[60];
            if (numArray[1] == 0)
            {
                int num2 = 0;
                int num3 = 0;
                int index = 0;
                num5 = 0;
                while (num5 < DBitLengthCounts.Length)
                {
                    int num6 = DBitLengthCounts[num5];
                    int num7 = 0;
                    while (num7 < num6)
                    {
                        numArray[index] = num2;
                        buffer[index] = (byte) num3;
                        num7++;
                        index++;
                        num2 += ((int) 1) << num3;
                    }
                    num5++;
                    num3++;
                }
            }
            this.fileExtracted = true;
            if (!base.suspended)
            {
                this.unpInitData(solid);
                if (!base.unpReadBuf() || !((solid && this.tablesRead) || this.readTables()))
                {
                    return;
                }
            }
            if (this.ppmError)
            {
                return;
            }
        Label_0751:
            flag2 = true;
            base.unpPtr &= Compress.MAXWINMASK;
            if ((base.inAddr <= base.readBorder) || base.unpReadBuf())
            {
                int num10;
                int num11;
                if ((((base.wrPtr - base.unpPtr) & Compress.MAXWINMASK) < 260) && (base.wrPtr != base.unpPtr))
                {
                    this.UnpWriteBuf();
                    if (base.destUnpSize <= 0L)
                    {
                        return;
                    }
                    if (base.suspended)
                    {
                        this.fileExtracted = false;
                        return;
                    }
                }
                if (this.unpBlockType == BlockTypes.BLOCK_PPM)
                {
                    int num8 = this.ppm.decodeChar();
                    if (num8 == -1)
                    {
                        this.ppmError = true;
                        goto Label_0759;
                    }
                    if (num8 == this.ppmEscChar)
                    {
                        switch (this.ppm.decodeChar())
                        {
                            case 0:
                                if (this.readTables())
                                {
                                    goto Label_0751;
                                }
                                goto Label_0759;

                            case 2:
                            case -1:
                                goto Label_0759;

                            case 3:
                                if (this.readVMCodePPM())
                                {
                                    goto Label_0751;
                                }
                                goto Label_0759;

                            case 4:
                            {
                                num10 = 0;
                                num11 = 0;
                                bool flag = false;
                                for (num5 = 0; (num5 < 4) && !flag; num5++)
                                {
                                    int num12 = this.ppm.decodeChar();
                                    if (num12 == -1)
                                    {
                                        flag = true;
                                    }
                                    else if (num5 == 3)
                                    {
                                        num11 = num12 & 0xff;
                                    }
                                    else
                                    {
                                        num10 = (num10 << 8) + (num12 & 0xff);
                                    }
                                }
                                if (flag)
                                {
                                    goto Label_0759;
                                }
                                this.copyString(num11 + 0x20, num10 + 2);
                                goto Label_0751;
                            }
                            case 5:
                                num11 = this.ppm.decodeChar();
                                if (num11 == -1)
                                {
                                    goto Label_0759;
                                }
                                this.copyString(num11 + 4, 1);
                                goto Label_0751;
                        }
                    }
                    base.window[base.unpPtr++] = (byte) num8;
                }
                else
                {
                    int num13 = UnpackUtility.decodeNumber(this, base.LD);
                    if (num13 < 0x100)
                    {
                        base.window[base.unpPtr++] = (byte) num13;
                    }
                    else
                    {
                        int num;
                        if (num13 >= 0x10f)
                        {
                            num11 = Unpack20.LDecode[num13 -= 0x10f] + 3;
                            num = Unpack20.LBits[num13];
                            if (num > 0)
                            {
                                num11 += Utility.URShift(base.GetBits(), (int) (0x10 - num));
                                base.AddBits(num);
                            }
                            int num14 = UnpackUtility.decodeNumber(this, base.DD);
                            num10 = numArray[num14] + 1;
                            num = buffer[num14];
                            if (num > 0)
                            {
                                if (num14 > 9)
                                {
                                    if (num > 4)
                                    {
                                        num10 += Utility.URShift(base.GetBits(), (int) (20 - num)) << 4;
                                        base.AddBits(num - 4);
                                    }
                                    if (this.lowDistRepCount > 0)
                                    {
                                        this.lowDistRepCount--;
                                        num10 += this.prevLowDist;
                                    }
                                    else
                                    {
                                        int num15 = UnpackUtility.decodeNumber(this, base.LDD);
                                        if (num15 == 0x10)
                                        {
                                            this.lowDistRepCount = 15;
                                            num10 += this.prevLowDist;
                                        }
                                        else
                                        {
                                            num10 += num15;
                                            this.prevLowDist = num15;
                                        }
                                    }
                                }
                                else
                                {
                                    num10 += Utility.URShift(base.GetBits(), (int) (0x10 - num));
                                    base.AddBits(num);
                                }
                            }
                            if (num10 >= 0x2000)
                            {
                                num11++;
                                if (num10 >= 0x40000L)
                                {
                                    num11++;
                                }
                            }
                            this.insertOldDist(num10);
                            this.insertLastMatch(num11, num10);
                            this.copyString(num11, num10);
                        }
                        else
                        {
                            switch (num13)
                            {
                                case 0x100:
                                    if (this.readEndOfBlock())
                                    {
                                        goto Label_0751;
                                    }
                                    goto Label_0759;

                                case 0x101:
                                    if (this.readVMCode())
                                    {
                                        goto Label_0751;
                                    }
                                    goto Label_0759;

                                case 0x102:
                                    if (base.lastLength != 0)
                                    {
                                        this.copyString(base.lastLength, base.lastDist);
                                    }
                                    goto Label_0751;
                            }
                            if (num13 < 0x107)
                            {
                                int num16 = num13 - 0x103;
                                num10 = base.oldDist[num16];
                                for (num5 = num16; num5 > 0; num5--)
                                {
                                    base.oldDist[num5] = base.oldDist[num5 - 1];
                                }
                                base.oldDist[0] = num10;
                                int num17 = UnpackUtility.decodeNumber(this, base.RD);
                                num11 = Unpack20.LDecode[num17] + 2;
                                num = Unpack20.LBits[num17];
                                if (num > 0)
                                {
                                    num11 += Utility.URShift(base.GetBits(), (int) (0x10 - num));
                                    base.AddBits(num);
                                }
                                this.insertLastMatch(num11, num10);
                                this.copyString(num11, num10);
                            }
                            else if (num13 < 0x110)
                            {
                                num10 = Unpack20.SDDecode[num13 -= 0x107] + 1;
                                num = Unpack20.SDBits[num13];
                                if (num > 0)
                                {
                                    num10 += Utility.URShift(base.GetBits(), (int) (0x10 - num));
                                    base.AddBits(num);
                                }
                                this.insertOldDist(num10);
                                this.insertLastMatch(2, num10);
                                this.copyString(2, num10);
                            }
                        }
                    }
                }
                goto Label_0751;
            }
        Label_0759:
            this.UnpWriteBuf();
        }

        protected internal override void unpInitData(bool solid)
        {
            if (!solid)
            {
                this.tablesRead = false;
                Utility.Fill<int>(base.oldDist, 0);
                base.oldDistPtr = 0;
                base.lastDist = 0;
                base.lastLength = 0;
                Utility.Fill<byte>(this.unpOldTable, 0);
                base.unpPtr = 0;
                base.wrPtr = 0;
                this.ppmEscChar = 2;
                this.initFilters();
            }
            base.InitBitInput();
            this.ppmError = false;
            this.writtenFileSize = 0L;
            base.readTop = 0;
            base.readBorder = 0;
            base.unpInitData20(solid);
        }

        private void UnpWriteArea(int startPtr, int endPtr)
        {
            if (endPtr != startPtr)
            {
                base.unpSomeRead = true;
            }
            if (endPtr < startPtr)
            {
                this.UnpWriteData(base.window, startPtr, -startPtr & Compress.MAXWINMASK);
                this.UnpWriteData(base.window, 0, endPtr);
                base.unpAllBuf = true;
            }
            else
            {
                this.UnpWriteData(base.window, startPtr, endPtr - startPtr);
            }
        }

        private void UnpWriteBuf()
        {
            int wrPtr = base.wrPtr;
            int num2 = (base.unpPtr - wrPtr) & Compress.MAXWINMASK;
            for (int i = 0; i < this.prgStack.Count; i++)
            {
                UnpackFilter filter = this.prgStack[i];
                if (filter != null)
                {
                    if (filter.NextWindow)
                    {
                        filter.NextWindow = false;
                        continue;
                    }
                    int blockStart = filter.BlockStart;
                    int blockLength = filter.BlockLength;
                    if (((blockStart - wrPtr) & Compress.MAXWINMASK) < num2)
                    {
                        if (wrPtr != blockStart)
                        {
                            this.UnpWriteArea(wrPtr, blockStart);
                            wrPtr = blockStart;
                            num2 = (base.unpPtr - wrPtr) & Compress.MAXWINMASK;
                        }
                        if (blockLength <= num2)
                        {
                            int num8;
                            int dataSize = (blockStart + blockLength) & Compress.MAXWINMASK;
                            if ((blockStart < dataSize) || (dataSize == 0))
                            {
                                this.rarVM.setMemory(0, base.window, blockStart, blockLength);
                            }
                            else
                            {
                                int num7 = 0x400000 - blockStart;
                                this.rarVM.setMemory(0, base.window, blockStart, num7);
                                this.rarVM.setMemory(num7, base.window, 0, dataSize);
                            }
                            VMPreparedProgram program = this.filters[filter.ParentFilter].Program;
                            VMPreparedProgram prg = filter.Program;
                            if (program.GlobalData.Count > 0x40)
                            {
                                prg.GlobalData.Clear();
                                num8 = 0;
                                while (num8 < (program.GlobalData.Count - 0x40))
                                {
                                    prg.GlobalData[0x40 + num8] = program.GlobalData[0x40 + num8];
                                    num8++;
                                }
                            }
                            this.ExecuteCode(prg);
                            if (prg.GlobalData.Count > 0x40)
                            {
                                if (program.GlobalData.Count < prg.GlobalData.Count)
                                {
                                    Utility.SetSize(program.GlobalData, prg.GlobalData.Count);
                                }
                                num8 = 0;
                                while (num8 < (prg.GlobalData.Count - 0x40))
                                {
                                    program.GlobalData[0x40 + num8] = prg.GlobalData[0x40 + num8];
                                    num8++;
                                }
                            }
                            else
                            {
                                program.GlobalData.Clear();
                            }
                            int filteredDataOffset = prg.FilteredDataOffset;
                            int filteredDataSize = prg.FilteredDataSize;
                            byte[] data = new byte[filteredDataSize];
                            num8 = 0;
                            while (num8 < filteredDataSize)
                            {
                                data[num8] = this.rarVM.Mem[filteredDataOffset + num8];
                                num8++;
                            }
                            this.prgStack[i] = null;
                            while ((i + 1) < this.prgStack.Count)
                            {
                                UnpackFilter filter2 = this.prgStack[i + 1];
                                if ((((filter2 == null) || (filter2.BlockStart != blockStart)) || (filter2.BlockLength != filteredDataSize)) || filter2.NextWindow)
                                {
                                    break;
                                }
                                this.rarVM.setMemory(0, data, 0, filteredDataSize);
                                VMPreparedProgram program3 = this.filters[filter2.ParentFilter].Program;
                                VMPreparedProgram program4 = filter2.Program;
                                if (program3.GlobalData.Count > 0x40)
                                {
                                    Utility.SetSize(program4.GlobalData, program3.GlobalData.Count);
                                    num8 = 0;
                                    while (num8 < (program3.GlobalData.Count - 0x40))
                                    {
                                        program4.GlobalData[0x40 + num8] = program3.GlobalData[0x40 + num8];
                                        num8++;
                                    }
                                }
                                this.ExecuteCode(program4);
                                if (program4.GlobalData.Count > 0x40)
                                {
                                    if (program3.GlobalData.Count < program4.GlobalData.Count)
                                    {
                                        Utility.SetSize(program3.GlobalData, program4.GlobalData.Count);
                                    }
                                    num8 = 0;
                                    while (num8 < (program4.GlobalData.Count - 0x40))
                                    {
                                        program3.GlobalData[0x40 + num8] = program4.GlobalData[0x40 + num8];
                                        num8++;
                                    }
                                }
                                else
                                {
                                    program3.GlobalData.Clear();
                                }
                                filteredDataOffset = program4.FilteredDataOffset;
                                filteredDataSize = program4.FilteredDataSize;
                                data = new byte[filteredDataSize];
                                for (num8 = 0; num8 < filteredDataSize; num8++)
                                {
                                    data[num8] = program4.GlobalData[filteredDataOffset + num8];
                                }
                                i++;
                                this.prgStack[i] = null;
                            }
                            base.writeStream.Write(data, 0, filteredDataSize);
                            base.unpSomeRead = true;
                            this.writtenFileSize += filteredDataSize;
                            base.destUnpSize -= filteredDataSize;
                            wrPtr = dataSize;
                            num2 = (base.unpPtr - wrPtr) & Compress.MAXWINMASK;
                            continue;
                        }
                        for (int j = i; j < this.prgStack.Count; j++)
                        {
                            UnpackFilter filter3 = this.prgStack[j];
                            if ((filter3 != null) && filter3.NextWindow)
                            {
                                filter3.NextWindow = false;
                            }
                        }
                        base.wrPtr = wrPtr;
                        return;
                    }
                }
            }
            this.UnpWriteArea(wrPtr, base.unpPtr);
            base.wrPtr = base.unpPtr;
        }

        private void UnpWriteData(byte[] data, int offset, int size)
        {
            if (base.destUnpSize > 0L)
            {
                int count = size;
                if (count > base.destUnpSize)
                {
                    count = (int) base.destUnpSize;
                }
                base.writeStream.Write(data, offset, count);
                this.writtenFileSize += size;
                base.destUnpSize -= size;
            }
        }

        private void unstoreFile()
        {
            bool flag;
            byte[] buffer = new byte[0x10000];
        Label_0096:
            flag = true;
            int count = base.readStream.Read(buffer, 0, (int) Math.Min((long) buffer.Length, base.destUnpSize));
            switch (count)
            {
                case 0:
                case -1:
                    break;

                default:
                    count = (count < base.destUnpSize) ? count : ((int) base.destUnpSize);
                    base.writeStream.Write(buffer, 0, count);
                    if (base.destUnpSize >= 0L)
                    {
                        base.destUnpSize -= count;
                    }
                    if (base.suspended)
                    {
                        break;
                    }
                    goto Label_0096;
            }
        }

        public int Char
        {
            get
            {
                if (base.inAddr > 0x7fe2)
                {
                    base.unpReadBuf();
                }
                return (base.InBuf[base.inAddr++] & 0xff);
            }
        }

        public long DestSize
        {
            get
            {
                return base.destUnpSize;
            }
            set
            {
                base.destUnpSize = value;
                this.fileExtracted = false;
            }
        }

        public bool FileExtracted
        {
            get
            {
                return this.fileExtracted;
            }
        }

        public int PpmEscChar
        {
            get
            {
                return this.ppmEscChar;
            }
            set
            {
                this.ppmEscChar = value;
            }
        }

        public bool Suspended
        {
            set
            {
                base.suspended = value;
            }
        }
    }
}

