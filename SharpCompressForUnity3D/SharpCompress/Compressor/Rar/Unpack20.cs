namespace SharpCompress.Compressor.Rar
{
    using SharpCompress;
    using SharpCompress.Compressor.Rar.decode;
    using System;

    internal abstract class Unpack20 : Unpack15
    {
        private readonly AudioVariables[] AudV = new AudioVariables[4];
        protected internal BitDecode BD = new BitDecode();
        public static readonly int[] DBits = new int[] { 
            0, 0, 0, 0, 1, 1, 2, 2, 3, 3, 4, 4, 5, 5, 6, 6, 
            7, 7, 8, 8, 9, 9, 10, 10, 11, 11, 12, 12, 13, 13, 14, 14, 
            15, 15, 0x10, 0x10, 0x10, 0x10, 0x10, 0x10, 0x10, 0x10, 0x10, 0x10, 0x10, 0x10, 0x10, 0x10
         };
        protected internal DistDecode DD = new DistDecode();
        public static readonly int[] DDecode = new int[] { 
            0, 1, 2, 3, 4, 6, 8, 12, 0x10, 0x18, 0x20, 0x30, 0x40, 0x60, 0x80, 0xc0, 
            0x100, 0x180, 0x200, 0x300, 0x400, 0x600, 0x800, 0xc00, 0x1000, 0x1800, 0x2000, 0x3000, 0x4000, 0x6000, 0x8000, 0xc000, 
            0x10000, 0x18000, 0x20000, 0x30000, 0x40000, 0x50000, 0x60000, 0x70000, 0x80000, 0x90000, 0xa0000, 0xb0000, 0xc0000, 0xd0000, 0xe0000, 0xf0000
         };
        public static readonly byte[] LBits = new byte[] { 
            0, 0, 0, 0, 0, 0, 0, 0, 1, 1, 1, 1, 2, 2, 2, 2, 
            3, 3, 3, 3, 4, 4, 4, 4, 5, 5, 5, 5
         };
        protected internal LitDecode LD = new LitDecode();
        protected internal LowDistDecode LDD = new LowDistDecode();
        public static readonly int[] LDecode = new int[] { 
            0, 1, 2, 3, 4, 5, 6, 7, 8, 10, 12, 14, 0x10, 20, 0x18, 0x1c, 
            0x20, 40, 0x30, 0x38, 0x40, 80, 0x60, 0x70, 0x80, 160, 0xc0, 0xe0
         };
        protected internal MultDecode[] MD = new MultDecode[4];
        protected internal RepDecode RD = new RepDecode();
        public static readonly int[] SDBits = new int[] { 2, 2, 3, 4, 5, 6, 6, 6 };
        public static readonly int[] SDDecode = new int[] { 0, 4, 8, 0x10, 0x20, 0x40, 0x80, 0xc0 };
        protected internal int UnpAudioBlock;
        protected internal int UnpChannelDelta;
        protected internal int UnpChannels;
        protected internal int UnpCurChannel;
        protected internal byte[] UnpOldTable20;

        public Unpack20()
        {
            this.InitBlock();
        }

        private void CopyString20(int Length, int Distance)
        {
            base.lastDist = base.oldDist[base.oldDistPtr++ & 3] = Distance;
            base.lastLength = Length;
            base.destUnpSize -= Length;
            int num = base.unpPtr - Distance;
            if ((num >= 0x3ffed4) || (base.unpPtr >= 0x3ffed4))
            {
                while (Length-- != 0)
                {
                    base.window[base.unpPtr] = base.window[num++ & Compress.MAXWINMASK];
                    base.unpPtr = (base.unpPtr + 1) & Compress.MAXWINMASK;
                }
            }
            else
            {
                base.window[base.unpPtr++] = base.window[num++];
                base.window[base.unpPtr++] = base.window[num++];
                while (Length > 2)
                {
                    Length--;
                    base.window[base.unpPtr++] = base.window[num++];
                }
            }
        }

        private byte DecodeAudio(int Delta)
        {
            AudioVariables variables = this.AudV[this.UnpCurChannel];
            variables.ByteCount++;
            variables.D4 = variables.D3;
            variables.D3 = variables.D2;
            variables.D2 = variables.LastDelta - variables.D1;
            variables.D1 = variables.LastDelta;
            int number = (8 * variables.LastChar) + (variables.K1 * variables.D1);
            number += (variables.K2 * variables.D2) + (variables.K3 * variables.D3);
            number += (variables.K4 * variables.D4) + (variables.K5 * this.UnpChannelDelta);
            number = Utility.URShift(number, 3) & 0xff;
            int num2 = number - Delta;
            int num3 = ((byte) Delta) << 3;
            variables.Dif[0] += Math.Abs(num3);
            variables.Dif[1] += Math.Abs((int) (num3 - variables.D1));
            variables.Dif[2] += Math.Abs((int) (num3 + variables.D1));
            variables.Dif[3] += Math.Abs((int) (num3 - variables.D2));
            variables.Dif[4] += Math.Abs((int) (num3 + variables.D2));
            variables.Dif[5] += Math.Abs((int) (num3 - variables.D3));
            variables.Dif[6] += Math.Abs((int) (num3 + variables.D3));
            variables.Dif[7] += Math.Abs((int) (num3 - variables.D4));
            variables.Dif[8] += Math.Abs((int) (num3 + variables.D4));
            variables.Dif[9] += Math.Abs((int) (num3 - this.UnpChannelDelta));
            variables.Dif[10] += Math.Abs((int) (num3 + this.UnpChannelDelta));
            variables.LastDelta = (byte) (num2 - variables.LastChar);
            this.UnpChannelDelta = variables.LastDelta;
            variables.LastChar = num2;
            if ((variables.ByteCount & 0x1f) == 0)
            {
                int num4 = variables.Dif[0];
                int num5 = 0;
                variables.Dif[0] = 0;
                for (int i = 1; i < variables.Dif.Length; i++)
                {
                    if (variables.Dif[i] < num4)
                    {
                        num4 = variables.Dif[i];
                        num5 = i;
                    }
                    variables.Dif[i] = 0;
                }
                switch (num5)
                {
                    case 1:
                        if (variables.K1 >= -16)
                        {
                            variables.K1--;
                        }
                        break;

                    case 2:
                        if (variables.K1 < 0x10)
                        {
                            variables.K1++;
                        }
                        break;

                    case 3:
                        if (variables.K2 >= -16)
                        {
                            variables.K2--;
                        }
                        break;

                    case 4:
                        if (variables.K2 < 0x10)
                        {
                            variables.K2++;
                        }
                        break;

                    case 5:
                        if (variables.K3 >= -16)
                        {
                            variables.K3--;
                        }
                        break;

                    case 6:
                        if (variables.K3 < 0x10)
                        {
                            variables.K3++;
                        }
                        break;

                    case 7:
                        if (variables.K4 >= -16)
                        {
                            variables.K4--;
                        }
                        break;

                    case 8:
                        if (variables.K4 < 0x10)
                        {
                            variables.K4++;
                        }
                        break;

                    case 9:
                        if (variables.K5 >= -16)
                        {
                            variables.K5--;
                        }
                        break;

                    case 10:
                        if (variables.K5 < 0x10)
                        {
                            variables.K5++;
                        }
                        break;
                }
            }
            return (byte) num2;
        }

        private int decodeNumber(Decode RD)
        {
            return UnpackUtility.decodeNumber(this, RD);
        }

        private void InitBlock()
        {
            this.UnpOldTable20 = new byte[0x404];
        }

        private void ReadLastTables()
        {
            if (base.readTop >= (base.inAddr + 5))
            {
                if (this.UnpAudioBlock != 0)
                {
                    if (this.decodeNumber(this.MD[this.UnpCurChannel]) == 0x100)
                    {
                        this.ReadTables20();
                    }
                }
                else if (this.decodeNumber(this.LD) == 0x10d)
                {
                    this.ReadTables20();
                }
            }
        }

        private bool ReadTables20()
        {
            int num;
            int num3;
            byte[] lenTab = new byte[0x13];
            byte[] buffer2 = new byte[0x404];
            if ((base.inAddr > (base.readTop - 0x19)) && !base.unpReadBuf())
            {
                return false;
            }
            int bits = base.GetBits();
            this.UnpAudioBlock = bits & 0x8000;
            if (0 == (bits & 0x4000))
            {
                Utility.Fill<byte>(this.UnpOldTable20, 0);
            }
            base.AddBits(2);
            if (this.UnpAudioBlock != 0)
            {
                this.UnpChannels = (Utility.URShift(bits, 12) & 3) + 1;
                if (this.UnpCurChannel >= this.UnpChannels)
                {
                    this.UnpCurChannel = 0;
                }
                base.AddBits(2);
                num = 0x101 * this.UnpChannels;
            }
            else
            {
                num = 0x176;
            }
            for (num3 = 0; num3 < 0x13; num3++)
            {
                lenTab[num3] = (byte) Utility.URShift(base.GetBits(), 12);
                base.AddBits(4);
            }
            UnpackUtility.makeDecodeTables(lenTab, 0, this.BD, 0x13);
            num3 = 0;
            while (num3 < num)
            {
                if ((base.inAddr > (base.readTop - 5)) && !base.unpReadBuf())
                {
                    return false;
                }
                int num5 = this.decodeNumber(this.BD);
                if (num5 < 0x10)
                {
                    buffer2[num3] = (byte) ((num5 + this.UnpOldTable20[num3]) & 15);
                    num3++;
                }
                else
                {
                    int num2;
                    if (num5 == 0x10)
                    {
                        num2 = Utility.URShift(base.GetBits(), 14) + 3;
                        base.AddBits(2);
                        while ((num2-- > 0) && (num3 < num))
                        {
                            buffer2[num3] = buffer2[num3 - 1];
                            num3++;
                        }
                    }
                    else
                    {
                        if (num5 == 0x11)
                        {
                            num2 = Utility.URShift(base.GetBits(), 13) + 3;
                            base.AddBits(3);
                        }
                        else
                        {
                            num2 = Utility.URShift(base.GetBits(), 9) + 11;
                            base.AddBits(7);
                        }
                        while ((num2-- > 0) && (num3 < num))
                        {
                            buffer2[num3++] = 0;
                        }
                    }
                }
            }
            if (base.inAddr <= base.readTop)
            {
                if (this.UnpAudioBlock != 0)
                {
                    for (num3 = 0; num3 < this.UnpChannels; num3++)
                    {
                        UnpackUtility.makeDecodeTables(buffer2, num3 * 0x101, this.MD[num3], 0x101);
                    }
                }
                else
                {
                    UnpackUtility.makeDecodeTables(buffer2, 0, this.LD, 0x12a);
                    UnpackUtility.makeDecodeTables(buffer2, 0x12a, this.DD, 0x30);
                    UnpackUtility.makeDecodeTables(buffer2, 0x15a, this.RD, 0x1c);
                }
                for (int i = 0; i < this.UnpOldTable20.Length; i++)
                {
                    this.UnpOldTable20[i] = buffer2[i];
                }
            }
            return true;
        }

        internal void unpack20(bool solid)
        {
            if (base.suspended)
            {
                base.unpPtr = base.wrPtr;
            }
            else
            {
                this.unpInitData(solid);
                if (!base.unpReadBuf() || (!solid && !this.ReadTables20()))
                {
                    return;
                }
                base.destUnpSize -= 1L;
            }
            while (base.destUnpSize >= 0L)
            {
                base.unpPtr &= Compress.MAXWINMASK;
                if ((base.inAddr > (base.readTop - 30)) && !base.unpReadBuf())
                {
                    break;
                }
                if ((((base.wrPtr - base.unpPtr) & Compress.MAXWINMASK) < 270) && (base.wrPtr != base.unpPtr))
                {
                    base.oldUnpWriteBuf();
                    if (base.suspended)
                    {
                        return;
                    }
                }
                if (this.UnpAudioBlock != 0)
                {
                    int delta = this.decodeNumber(this.MD[this.UnpCurChannel]);
                    if (delta == 0x100)
                    {
                        if (this.ReadTables20())
                        {
                            continue;
                        }
                        break;
                    }
                    base.window[base.unpPtr++] = this.DecodeAudio(delta);
                    if (++this.UnpCurChannel == this.UnpChannels)
                    {
                        this.UnpCurChannel = 0;
                    }
                    base.destUnpSize -= 1L;
                }
                else
                {
                    int num;
                    int num4;
                    int num6;
                    int index = this.decodeNumber(this.LD);
                    if (index < 0x100)
                    {
                        base.window[base.unpPtr++] = (byte) index;
                        base.destUnpSize -= 1L;
                        continue;
                    }
                    if (index > 0x10d)
                    {
                        num4 = LDecode[index -= 270] + 3;
                        num = LBits[index];
                        if (num > 0)
                        {
                            num4 += Utility.URShift(base.GetBits(), (int) (0x10 - num));
                            base.AddBits(num);
                        }
                        int num5 = this.decodeNumber(this.DD);
                        num6 = DDecode[num5] + 1;
                        num = DBits[num5];
                        if (num > 0)
                        {
                            num6 += Utility.URShift(base.GetBits(), (int) (0x10 - num));
                            base.AddBits(num);
                        }
                        if (num6 >= 0x2000)
                        {
                            num4++;
                            if (num6 >= 0x40000L)
                            {
                                num4++;
                            }
                        }
                        this.CopyString20(num4, num6);
                        continue;
                    }
                    if (index == 0x10d)
                    {
                        if (this.ReadTables20())
                        {
                            continue;
                        }
                        break;
                    }
                    if (index == 0x100)
                    {
                        this.CopyString20(base.lastLength, base.lastDist);
                        continue;
                    }
                    if (index < 0x105)
                    {
                        num6 = base.oldDist[(base.oldDistPtr - (index - 0x100)) & 3];
                        int num7 = this.decodeNumber(this.RD);
                        num4 = LDecode[num7] + 2;
                        num = LBits[num7];
                        if (num > 0)
                        {
                            num4 += Utility.URShift(base.GetBits(), (int) (0x10 - num));
                            base.AddBits(num);
                        }
                        if (num6 >= 0x101)
                        {
                            num4++;
                            if (num6 >= 0x2000)
                            {
                                num4++;
                                if (num6 >= 0x40000)
                                {
                                    num4++;
                                }
                            }
                        }
                        this.CopyString20(num4, num6);
                        continue;
                    }
                    if (index < 270)
                    {
                        num6 = SDDecode[index -= 0x105] + 1;
                        num = SDBits[index];
                        if (num > 0)
                        {
                            num6 += Utility.URShift(base.GetBits(), (int) (0x10 - num));
                            base.AddBits(num);
                        }
                        this.CopyString20(2, num6);
                    }
                }
            }
            this.ReadLastTables();
            base.oldUnpWriteBuf();
        }

        protected void unpInitData20(bool Solid)
        {
            if (!Solid)
            {
                this.UnpChannelDelta = this.UnpCurChannel = 0;
                this.UnpChannels = 1;
                this.AudV[0] = new AudioVariables();
                this.AudV[1] = new AudioVariables();
                this.AudV[2] = new AudioVariables();
                this.AudV[3] = new AudioVariables();
                Utility.Fill<byte>(this.UnpOldTable20, 0);
            }
        }
    }
}

