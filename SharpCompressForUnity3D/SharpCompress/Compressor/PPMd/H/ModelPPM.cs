namespace SharpCompress.Compressor.PPMd.H
{
    using SharpCompress;
    using SharpCompress.Compressor.LZMA.RangeCoder;
    using SharpCompress.Compressor.Rar;
    using System;
    using System.IO;
    using System.Text;

    internal class ModelPPM
    {
        public static readonly int BIN_SCALE = (((int) 1) << TOT_BITS);
        private int[][] binSumm = new int[0x80][];
        private int[] charMask = new int[0x100];
        private RangeCoder coder;
        private SEE2Context dummySEE2Cont;
        private int escCount;
        private SharpCompress.Compressor.PPMd.H.State foundState;
        private int[] HB2Flag = new int[0x100];
        private int hiBitsFlag;
        private static int[] InitBinEsc = new int[] { 0x3cdd, 0x1f3f, 0x59bf, 0x48f3, 0x64a1, 0x5abc, 0x6632, 0x6051 };
        private int initEsc;
        private int initRL;
        public const int INT_BITS = 7;
        public static readonly int INTERVAL = 0x80;
        public const int MAX_FREQ = 0x7c;
        public const int MAX_O = 0x40;
        private PPMContext maxContext;
        private int maxOrder;
        private PPMContext minContext;
        private int[] NS2BSIndx = new int[0x100];
        private int[] NS2Indx = new int[0x100];
        private int numMasked;
        private int orderFall;
        public const int PERIOD_BITS = 7;
        private int prevSuccess;
        private int[] ps = new int[0x40];
        private int runLength;
        private SEE2Context[][] SEE2Cont = new SEE2Context[0x19][];
        private SubAllocator subAlloc = new SubAllocator();
        private PPMContext tempPPMContext1 = new PPMContext(null);
        private PPMContext tempPPMContext2 = new PPMContext(null);
        private PPMContext tempPPMContext3 = new PPMContext(null);
        private PPMContext tempPPMContext4 = new PPMContext(null);
        private SharpCompress.Compressor.PPMd.H.State tempState1 = new SharpCompress.Compressor.PPMd.H.State(null);
        private SharpCompress.Compressor.PPMd.H.State tempState2 = new SharpCompress.Compressor.PPMd.H.State(null);
        private SharpCompress.Compressor.PPMd.H.State tempState3 = new SharpCompress.Compressor.PPMd.H.State(null);
        private SharpCompress.Compressor.PPMd.H.State tempState4 = new SharpCompress.Compressor.PPMd.H.State(null);
        private StateRef tempStateRef1 = new StateRef();
        private StateRef tempStateRef2 = new StateRef();
        public static readonly int TOT_BITS = 14;

        public ModelPPM()
        {
            this.InitBlock();
            this.minContext = null;
            this.maxContext = null;
        }

        private void clearMask()
        {
            this.escCount = 1;
            Utility.Fill<int>(this.charMask, 0);
        }

        private int createSuccessors(bool Skip, SharpCompress.Compressor.PPMd.H.State p1)
        {
            StateRef firstState = this.tempStateRef2;
            SharpCompress.Compressor.PPMd.H.State pStats = this.tempState1.Initialize(this.Heap);
            PPMContext context = this.tempPPMContext1.Initialize(this.Heap);
            context.Address = this.minContext.Address;
            PPMContext context2 = this.tempPPMContext2.Initialize(this.Heap);
            context2.Address = this.foundState.GetSuccessor();
            SharpCompress.Compressor.PPMd.H.State state2 = this.tempState2.Initialize(this.Heap);
            int num = 0;
            bool flag = false;
            if (!Skip)
            {
                this.ps[num++] = this.foundState.Address;
                if (context.getSuffix() == 0)
                {
                    flag = true;
                }
            }
            if (flag)
            {
                goto Label_01E0;
            }
            bool flag2 = false;
            if (p1.Address != 0)
            {
                state2.Address = p1.Address;
                context.Address = context.getSuffix();
                flag2 = true;
            }
        Label_00F2:
            if (!flag2)
            {
                context.Address = context.getSuffix();
                if (context.NumStats != 1)
                {
                    state2.Address = context.FreqData.GetStats();
                    if (state2.Symbol != this.foundState.Symbol)
                    {
                        do
                        {
                            state2.IncrementAddress();
                        }
                        while (state2.Symbol != this.foundState.Symbol);
                    }
                }
                else
                {
                    state2.Address = context.getOneState().Address;
                }
            }
            flag2 = false;
            if (state2.GetSuccessor() != context2.Address)
            {
                context.Address = state2.GetSuccessor();
            }
            else
            {
                this.ps[num++] = state2.Address;
                if (context.getSuffix() != 0)
                {
                    goto Label_00F2;
                }
            }
        Label_01E0:
            if (num != 0)
            {
                firstState.Symbol = this.Heap[context2.Address];
                firstState.SetSuccessor((int) (context2.Address + 1));
                if (context.NumStats != 1)
                {
                    if (context.Address <= this.subAlloc.PText)
                    {
                        return 0;
                    }
                    state2.Address = context.FreqData.GetStats();
                    if (state2.Symbol != firstState.Symbol)
                    {
                        do
                        {
                            state2.IncrementAddress();
                        }
                        while (state2.Symbol != firstState.Symbol);
                    }
                    int num2 = state2.Freq - 1;
                    int num3 = (context.FreqData.SummFreq - context.NumStats) - num2;
                    firstState.Freq = 1 + (((2 * num2) <= num3) ? (((5 * num2) > num3) ? 1 : 0) : ((((2 * num2) + (3 * num3)) - 1) / (2 * num3)));
                }
                else
                {
                    firstState.Freq = context.getOneState().Freq;
                }
                do
                {
                    pStats.Address = this.ps[--num];
                    context.Address = context.createChild(this, pStats, firstState);
                    if (context.Address == 0)
                    {
                        return 0;
                    }
                }
                while (num != 0);
            }
            return context.Address;
        }

        public virtual int decodeChar()
        {
            if ((this.minContext.Address <= this.subAlloc.PText) || (this.minContext.Address > this.subAlloc.HeapEnd))
            {
                return -1;
            }
            if (this.minContext.NumStats != 1)
            {
                if ((this.minContext.FreqData.GetStats() <= this.subAlloc.PText) || (this.minContext.FreqData.GetStats() > this.subAlloc.HeapEnd))
                {
                    return -1;
                }
                if (!this.minContext.decodeSymbol1(this))
                {
                    return -1;
                }
            }
            else
            {
                this.minContext.decodeBinSymbol(this);
            }
            this.coder.Decode();
            while (this.foundState.Address == 0)
            {
                this.coder.AriDecNormalize();
                do
                {
                    this.orderFall++;
                    this.minContext.Address = this.minContext.getSuffix();
                    if ((this.minContext.Address <= this.subAlloc.PText) || (this.minContext.Address > this.subAlloc.HeapEnd))
                    {
                        return -1;
                    }
                }
                while (this.minContext.NumStats == this.numMasked);
                if (!this.minContext.decodeSymbol2(this))
                {
                    return -1;
                }
                this.coder.Decode();
            }
            int symbol = this.foundState.Symbol;
            if ((this.orderFall == 0) && (this.foundState.GetSuccessor() > this.subAlloc.PText))
            {
                int successor = this.foundState.GetSuccessor();
                this.minContext.Address = successor;
                this.maxContext.Address = successor;
            }
            else
            {
                this.updateModel();
                if (this.escCount == 0)
                {
                    this.clearMask();
                }
            }
            this.coder.AriDecNormalize();
            return symbol;
        }

        public int decodeChar(SharpCompress.Compressor.LZMA.RangeCoder.Decoder decoder)
        {
            SharpCompress.Compressor.PPMd.H.State state;
            int num;
            int threshold;
            int num3;
            byte symbol;
            if (this.minContext.NumStats != 1)
            {
                state = this.tempState1.Initialize(this.Heap);
                state.Address = this.minContext.FreqData.GetStats();
                if ((threshold = (int) decoder.GetThreshold((uint) this.minContext.FreqData.SummFreq)) < (num3 = state.Freq))
                {
                    decoder.Decode(0, (uint) state.Freq);
                    symbol = (byte) state.Symbol;
                    this.minContext.update1_0(this, state.Address);
                    this.nextContext();
                    return symbol;
                }
                this.prevSuccess = 0;
                num = this.minContext.NumStats - 1;
                do
                {
                    state.IncrementAddress();
                    if ((num3 += state.Freq) > threshold)
                    {
                        decoder.Decode((uint) (num3 - state.Freq), (uint) state.Freq);
                        symbol = (byte) state.Symbol;
                        this.minContext.update1(this, state.Address);
                        this.nextContext();
                        return symbol;
                    }
                }
                while (--num > 0);
                if (threshold >= this.minContext.FreqData.SummFreq)
                {
                    return -2;
                }
                this.hiBitsFlag = this.HB2Flag[this.foundState.Symbol];
                decoder.Decode((uint) num3, (uint) (this.minContext.FreqData.SummFreq - num3));
                for (num = 0; num < 0x100; num++)
                {
                    this.charMask[num] = -1;
                }
                this.charMask[state.Symbol] = 0;
                num = this.minContext.NumStats - 1;
                do
                {
                    state.DecrementAddress();
                    this.charMask[state.Symbol] = 0;
                }
                while (--num > 0);
            }
            else
            {
                SharpCompress.Compressor.PPMd.H.State rs = this.tempState1.Initialize(this.Heap);
                rs.Address = this.minContext.getOneState().Address;
                this.hiBitsFlag = this.getHB2Flag()[this.foundState.Symbol];
                int index = rs.Freq - 1;
                int num6 = this.minContext.getArrayIndex(this, rs);
                int summ = this.binSumm[index][num6];
                if (decoder.DecodeBit((uint) summ, 14) == 0)
                {
                    this.binSumm[index][num6] = ((summ + INTERVAL) - this.minContext.getMean(summ, 7, 2)) & 0xffff;
                    this.foundState.Address = rs.Address;
                    symbol = (byte) rs.Symbol;
                    rs.IncrementFreq((rs.Freq < 0x80) ? 1 : 0);
                    this.prevSuccess = 1;
                    this.incRunLength(1);
                    this.nextContext();
                    return symbol;
                }
                summ = (summ - this.minContext.getMean(summ, 7, 2)) & 0xffff;
                this.binSumm[index][num6] = summ;
                this.initEsc = PPMContext.ExpEscape[Utility.URShift(summ, 10)];
                for (num = 0; num < 0x100; num++)
                {
                    this.charMask[num] = -1;
                }
                this.charMask[rs.Symbol] = 0;
                this.prevSuccess = 0;
            }
            while (true)
            {
                int num8;
                state = this.tempState1.Initialize(this.Heap);
                int numStats = this.minContext.NumStats;
                do
                {
                    this.orderFall++;
                    this.minContext.Address = this.minContext.getSuffix();
                    if ((this.minContext.Address <= this.subAlloc.PText) || (this.minContext.Address > this.subAlloc.HeapEnd))
                    {
                        return -1;
                    }
                }
                while (this.minContext.NumStats == numStats);
                num3 = 0;
                state.Address = this.minContext.FreqData.GetStats();
                num = 0;
                int num9 = this.minContext.NumStats - numStats;
                do
                {
                    int num11 = this.charMask[state.Symbol];
                    num3 += state.Freq & num11;
                    this.minContext.ps[num] = state.Address;
                    state.IncrementAddress();
                    num -= num11;
                }
                while (num != num9);
                SEE2Context context = this.minContext.makeEscFreq(this, numStats, out num8);
                num8 += num3;
                threshold = (int) decoder.GetThreshold((uint) num8);
                if (threshold < num3)
                {
                    SharpCompress.Compressor.PPMd.H.State state3 = this.tempState2.Initialize(this.Heap);
                    num3 = 0;
                    num = 0;
                    state3.Address = this.minContext.ps[num];
                    while ((num3 += state3.Freq) <= threshold)
                    {
                        num++;
                        state3.Address = this.minContext.ps[num];
                    }
                    state.Address = state3.Address;
                    decoder.Decode((uint) (num3 - state.Freq), (uint) state.Freq);
                    context.update();
                    symbol = (byte) state.Symbol;
                    this.minContext.update2(this, state.Address);
                    this.updateModel();
                    return symbol;
                }
                if (threshold >= num8)
                {
                    return -2;
                }
                decoder.Decode((uint) num3, (uint) (num8 - num3));
                context.Summ += num8;
                do
                {
                    state.Address = this.minContext.ps[--num];
                    this.charMask[state.Symbol] = 0;
                }
                while (num != 0);
            }
        }

        internal bool decodeInit(Unpack unpackRead, int escChar)
        {
            int maxOrder = unpackRead.Char & 0xff;
            bool flag = (maxOrder & 0x20) != 0;
            int num2 = 0;
            if (flag)
            {
                num2 = unpackRead.Char;
            }
            else if (this.subAlloc.GetAllocatedMemory() == 0)
            {
                return false;
            }
            if ((maxOrder & 0x40) != 0)
            {
                escChar = unpackRead.Char;
                unpackRead.PpmEscChar = escChar;
            }
            this.coder = new RangeCoder(unpackRead);
            if (flag)
            {
                maxOrder = (maxOrder & 0x1f) + 1;
                if (maxOrder > 0x10)
                {
                    maxOrder = 0x10 + ((maxOrder - 0x10) * 3);
                }
                if (maxOrder == 1)
                {
                    this.subAlloc.stopSubAllocator();
                    return false;
                }
                this.subAlloc.startSubAllocator((num2 + 1) << 20);
                this.minContext = new PPMContext(this.Heap);
                this.maxContext = new PPMContext(this.Heap);
                this.foundState = new SharpCompress.Compressor.PPMd.H.State(this.Heap);
                this.dummySEE2Cont = new SEE2Context();
                for (int i = 0; i < 0x19; i++)
                {
                    for (int j = 0; j < 0x10; j++)
                    {
                        this.SEE2Cont[i][j] = new SEE2Context();
                    }
                }
                this.startModelRare(maxOrder);
            }
            return (this.minContext.Address != 0);
        }

        internal bool decodeInit(Stream stream, int maxOrder, int maxMemory)
        {
            if (stream != null)
            {
                this.coder = new RangeCoder(stream);
            }
            if (maxOrder == 1)
            {
                this.subAlloc.stopSubAllocator();
                return false;
            }
            this.subAlloc.startSubAllocator(maxMemory);
            this.minContext = new PPMContext(this.Heap);
            this.maxContext = new PPMContext(this.Heap);
            this.foundState = new SharpCompress.Compressor.PPMd.H.State(this.Heap);
            this.dummySEE2Cont = new SEE2Context();
            for (int i = 0; i < 0x19; i++)
            {
                for (int j = 0; j < 0x10; j++)
                {
                    this.SEE2Cont[i][j] = new SEE2Context();
                }
            }
            this.startModelRare(maxOrder);
            return (this.minContext.Address != 0);
        }

        public virtual int[] getHB2Flag()
        {
            return this.HB2Flag;
        }

        public virtual int[] getNS2BSIndx()
        {
            return this.NS2BSIndx;
        }

        public virtual int[] getNS2Indx()
        {
            return this.NS2Indx;
        }

        public virtual SEE2Context[][] getSEE2Cont()
        {
            return this.SEE2Cont;
        }

        public virtual void incEscCount(int dEscCount)
        {
            this.EscCount += dEscCount;
        }

        public virtual void incRunLength(int dRunLength)
        {
            this.RunLength += dRunLength;
        }

        private void InitBlock()
        {
            for (int i = 0; i < 0x19; i++)
            {
                this.SEE2Cont[i] = new SEE2Context[0x10];
            }
            for (int j = 0; j < 0x80; j++)
            {
                this.binSumm[j] = new int[0x40];
            }
        }

        internal void nextContext()
        {
            int successor = this.foundState.GetSuccessor();
            if ((this.orderFall == 0) && (successor > this.subAlloc.PText))
            {
                this.minContext.Address = successor;
                this.maxContext.Address = successor;
            }
            else
            {
                this.updateModel();
            }
        }

        private void restartModelRare()
        {
            int num2;
            int num3;
            Utility.Fill<int>(this.charMask, 0);
            this.subAlloc.initSubAllocator();
            this.initRL = -((this.maxOrder < 12) ? this.maxOrder : 12) - 1;
            int stats = this.subAlloc.allocContext();
            this.minContext.Address = stats;
            this.maxContext.Address = stats;
            this.minContext.setSuffix(0);
            this.orderFall = this.maxOrder;
            this.minContext.NumStats = 0x100;
            this.minContext.FreqData.SummFreq = this.minContext.NumStats + 1;
            stats = this.subAlloc.allocUnits(0x80);
            this.foundState.Address = stats;
            this.minContext.FreqData.SetStats(stats);
            SharpCompress.Compressor.PPMd.H.State state = new SharpCompress.Compressor.PPMd.H.State(this.subAlloc.Heap);
            stats = this.minContext.FreqData.GetStats();
            this.runLength = this.initRL;
            this.prevSuccess = 0;
            for (num2 = 0; num2 < 0x100; num2++)
            {
                state.Address = stats + (num2 * 6);
                state.Symbol = num2;
                state.Freq = 1;
                state.SetSuccessor(0);
            }
            for (num2 = 0; num2 < 0x80; num2++)
            {
                num3 = 0;
                while (num3 < 8)
                {
                    for (int i = 0; i < 0x40; i += 8)
                    {
                        this.binSumm[num2][num3 + i] = BIN_SCALE - (InitBinEsc[num3] / (num2 + 2));
                    }
                    num3++;
                }
            }
            for (num2 = 0; num2 < 0x19; num2++)
            {
                for (num3 = 0; num3 < 0x10; num3++)
                {
                    this.SEE2Cont[num2][num3].Initialize((5 * num2) + 10);
                }
            }
        }

        private void startModelRare(int MaxOrder)
        {
            int num5;
            this.escCount = 1;
            this.maxOrder = MaxOrder;
            this.restartModelRare();
            this.NS2BSIndx[0] = 0;
            this.NS2BSIndx[1] = 2;
            for (num5 = 0; num5 < 9; num5++)
            {
                this.NS2BSIndx[2 + num5] = 4;
            }
            for (num5 = 0; num5 < 0xf5; num5++)
            {
                this.NS2BSIndx[11 + num5] = 6;
            }
            int index = 0;
            while (index < 3)
            {
                this.NS2Indx[index] = index;
                index++;
            }
            int num3 = index;
            int num2 = 1;
            int num4 = 1;
            while (index < 0x100)
            {
                this.NS2Indx[index] = num3;
                if (--num2 == 0)
                {
                    num2 = ++num4;
                    num3++;
                }
                index++;
            }
            for (num5 = 0; num5 < 0x40; num5++)
            {
                this.HB2Flag[num5] = 0;
            }
            for (num5 = 0; num5 < 0xc0; num5++)
            {
                this.HB2Flag[0x40 + num5] = 8;
            }
            this.dummySEE2Cont.Shift = 7;
        }

        public override string ToString()
        {
            StringBuilder builder = new StringBuilder();
            builder.Append("ModelPPM[");
            builder.Append("\n  numMasked=");
            builder.Append(this.numMasked);
            builder.Append("\n  initEsc=");
            builder.Append(this.initEsc);
            builder.Append("\n  orderFall=");
            builder.Append(this.orderFall);
            builder.Append("\n  maxOrder=");
            builder.Append(this.maxOrder);
            builder.Append("\n  runLength=");
            builder.Append(this.runLength);
            builder.Append("\n  initRL=");
            builder.Append(this.initRL);
            builder.Append("\n  escCount=");
            builder.Append(this.escCount);
            builder.Append("\n  prevSuccess=");
            builder.Append(this.prevSuccess);
            builder.Append("\n  foundState=");
            builder.Append(this.foundState);
            builder.Append("\n  coder=");
            builder.Append(this.coder);
            builder.Append("\n  subAlloc=");
            builder.Append(this.subAlloc);
            builder.Append("\n]");
            return builder.ToString();
        }

        private void updateModel()
        {
            StateRef ref2 = this.tempStateRef1;
            ref2.Values = this.foundState;
            SharpCompress.Compressor.PPMd.H.State state = this.tempState3.Initialize(this.Heap);
            SharpCompress.Compressor.PPMd.H.State state2 = this.tempState4.Initialize(this.Heap);
            PPMContext context = this.tempPPMContext3.Initialize(this.Heap);
            PPMContext successor = this.tempPPMContext4.Initialize(this.Heap);
            context.Address = this.minContext.getSuffix();
            if ((ref2.Freq < 0x1f) && (context.Address != 0))
            {
                if (context.NumStats != 1)
                {
                    state.Address = context.FreqData.GetStats();
                    if (state.Symbol != ref2.Symbol)
                    {
                        do
                        {
                            state.IncrementAddress();
                        }
                        while (state.Symbol != ref2.Symbol);
                        state2.Address = state.Address - 6;
                        if (state.Freq >= state2.Freq)
                        {
                            SharpCompress.Compressor.PPMd.H.State.PPMDSwap(state, state2);
                            state.DecrementAddress();
                        }
                    }
                    if (state.Freq < 0x73)
                    {
                        state.IncrementFreq(2);
                        context.FreqData.IncrementSummFreq(2);
                    }
                }
                else
                {
                    state.Address = context.getOneState().Address;
                    if (state.Freq < 0x20)
                    {
                        state.IncrementFreq(1);
                    }
                }
            }
            if (this.orderFall == 0)
            {
                this.foundState.SetSuccessor(this.createSuccessors(true, state));
                this.minContext.Address = this.foundState.GetSuccessor();
                this.maxContext.Address = this.foundState.GetSuccessor();
                if (this.minContext.Address == 0)
                {
                    this.updateModelRestart();
                }
            }
            else
            {
                this.subAlloc.Heap[this.subAlloc.PText] = (byte) ref2.Symbol;
                this.subAlloc.incPText();
                successor.Address = this.subAlloc.PText;
                if (this.subAlloc.PText >= this.subAlloc.FakeUnitsStart)
                {
                    this.updateModelRestart();
                }
                else
                {
                    if (ref2.GetSuccessor() != 0)
                    {
                        if (ref2.GetSuccessor() <= this.subAlloc.PText)
                        {
                            ref2.SetSuccessor(this.createSuccessors(false, state));
                            if (ref2.GetSuccessor() == 0)
                            {
                                this.updateModelRestart();
                                return;
                            }
                        }
                        if (--this.orderFall == 0)
                        {
                            successor.Address = ref2.GetSuccessor();
                            if (this.maxContext.Address != this.minContext.Address)
                            {
                                this.subAlloc.decPText(1);
                            }
                        }
                    }
                    else
                    {
                        this.foundState.SetSuccessor(successor.Address);
                        ref2.SetSuccessor(this.minContext);
                    }
                    int numStats = this.minContext.NumStats;
                    int num5 = (this.minContext.FreqData.SummFreq - numStats) - (ref2.Freq - 1);
                    context.Address = this.maxContext.Address;
                    while (context.Address != this.minContext.Address)
                    {
                        int number = context.NumStats;
                        if (number != 1)
                        {
                            if ((number & 1) == 0)
                            {
                                context.FreqData.SetStats(this.subAlloc.expandUnits(context.FreqData.GetStats(), Utility.URShift(number, 1)));
                                if (context.FreqData.GetStats() == 0)
                                {
                                    this.updateModelRestart();
                                    return;
                                }
                            }
                            int num6 = (((2 * number) < numStats) ? 1 : 0) + (2 * ((((4 * number) <= numStats) ? 1 : 0) & ((context.FreqData.SummFreq <= (8 * number)) ? 1 : 0)));
                            context.FreqData.IncrementSummFreq(num6);
                        }
                        else
                        {
                            state.Address = this.subAlloc.allocUnits(1);
                            if (state.Address == 0)
                            {
                                this.updateModelRestart();
                                return;
                            }
                            state.SetValues(context.getOneState());
                            context.FreqData.SetStats(state);
                            if (state.Freq < 30)
                            {
                                state.IncrementFreq(state.Freq);
                            }
                            else
                            {
                                state.Freq = 120;
                            }
                            context.FreqData.SummFreq = (state.Freq + this.initEsc) + ((numStats > 3) ? 1 : 0);
                        }
                        int dSummFreq = (2 * ref2.Freq) * (context.FreqData.SummFreq + 6);
                        int num4 = num5 + context.FreqData.SummFreq;
                        if (dSummFreq < (6 * num4))
                        {
                            dSummFreq = (1 + ((dSummFreq > num4) ? 1 : 0)) + ((dSummFreq >= (4 * num4)) ? 1 : 0);
                            context.FreqData.IncrementSummFreq(3);
                        }
                        else
                        {
                            dSummFreq = ((4 + ((dSummFreq >= (9 * num4)) ? 1 : 0)) + ((dSummFreq >= (12 * num4)) ? 1 : 0)) + ((dSummFreq >= (15 * num4)) ? 1 : 0);
                            context.FreqData.IncrementSummFreq(dSummFreq);
                        }
                        state.Address = context.FreqData.GetStats() + (number * 6);
                        state.SetSuccessor(successor);
                        state.Symbol = ref2.Symbol;
                        state.Freq = dSummFreq;
                        context.NumStats = ++number;
                        context.Address = context.getSuffix();
                    }
                    int num7 = ref2.GetSuccessor();
                    this.maxContext.Address = num7;
                    this.minContext.Address = num7;
                }
            }
        }

        private void updateModelRestart()
        {
            this.restartModelRare();
            this.escCount = 0;
        }

        public virtual int[][] BinSumm
        {
            get
            {
                return this.binSumm;
            }
        }

        public virtual int[] CharMask
        {
            get
            {
                return this.charMask;
            }
        }

        internal RangeCoder Coder
        {
            get
            {
                return this.coder;
            }
        }

        public virtual SEE2Context DummySEE2Cont
        {
            get
            {
                return this.dummySEE2Cont;
            }
        }

        public virtual int EscCount
        {
            get
            {
                return this.escCount;
            }
            set
            {
                this.escCount = value & 0xff;
            }
        }

        internal SharpCompress.Compressor.PPMd.H.State FoundState
        {
            get
            {
                return this.foundState;
            }
        }

        public virtual byte[] Heap
        {
            get
            {
                return this.subAlloc.Heap;
            }
        }

        public virtual int HiBitsFlag
        {
            get
            {
                return this.hiBitsFlag;
            }
            set
            {
                this.hiBitsFlag = value & 0xff;
            }
        }

        public virtual int InitEsc
        {
            get
            {
                return this.initEsc;
            }
            set
            {
                this.initEsc = value;
            }
        }

        public virtual int InitRL
        {
            get
            {
                return this.initRL;
            }
        }

        public virtual int NumMasked
        {
            get
            {
                return this.numMasked;
            }
            set
            {
                this.numMasked = value;
            }
        }

        public virtual int OrderFall
        {
            get
            {
                return this.orderFall;
            }
        }

        public virtual int PrevSuccess
        {
            get
            {
                return this.prevSuccess;
            }
            set
            {
                this.prevSuccess = value & 0xff;
            }
        }

        public virtual int RunLength
        {
            get
            {
                return this.runLength;
            }
            set
            {
                this.runLength = value;
            }
        }

        public SubAllocator SubAlloc
        {
            get
            {
                return this.subAlloc;
            }
        }
    }
}

