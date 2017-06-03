using System.Text;
using System.IO;
using SharpCompress.Compressor.Rar;

namespace SharpCompress.Compressor.PPMd.H
{
    internal class ModelPPM
    {
        private void InitBlock()
        {
            for (int i = 0; i < 25; i++)
            {
                SEE2Cont[i] = new SEE2Context[16];
            }
            for (int i2 = 0; i2 < 128; i2++)
            {
                binSumm[i2] = new int[64];
            }
        }
        public SubAllocator SubAlloc
        {
            get
            {
                return subAlloc;
            }

        }
        virtual public SEE2Context DummySEE2Cont
        {
            get
            {
                return dummySEE2Cont;
            }

        }
        virtual public int InitRL
        {
            get
            {
                return initRL;
            }

        }
        virtual public int EscCount
        {
            get
            {
                return escCount;
            }

            set
            {
                this.escCount = value & 0xff;
            }

        }
        virtual public int[] CharMask
        {
            get
            {
                return charMask;
            }

        }
        virtual public int NumMasked
        {
            get
            {
                return numMasked;
            }

            set
            {
                this.numMasked = value;
            }

        }
        virtual public int PrevSuccess
        {
            get
            {
                return prevSuccess;
            }

            set
            {
                this.prevSuccess = value & 0xff;
            }

        }
        virtual public int InitEsc
        {
            get
            {
                return initEsc;
            }

            set
            {
                this.initEsc = value;
            }

        }
        virtual public int RunLength
        {
            get
            {
                return runLength;
            }

            set
            {
                this.runLength = value;
            }

        }
        virtual public int HiBitsFlag
        {
            get
            {
                return hiBitsFlag;
            }

            set
            {
                this.hiBitsFlag = value & 0xff;
            }

        }
        virtual public int[][] BinSumm
        {
            get
            {
                return binSumm;
            }

        }
        internal RangeCoder Coder
        {
            get
            {
                return coder;
            }

        }
        internal State FoundState
        {
            get
            {
                return foundState;
            }

        }
        virtual public byte[] Heap
        {
            get
            {
                return subAlloc.Heap;
            }

        }
        virtual public int OrderFall
        {
            get
            {
                return orderFall;
            }

        }
        public const int MAX_O = 64; /* maximum allowed model order */

        public const int INT_BITS = 7;

        public const int PERIOD_BITS = 7;

        //UPGRADE_NOTE: Final was removed from the declaration of 'TOT_BITS '. "ms-help://MS.VSCC.v80/dv_commoner/local/redirect.htm?index='!DefaultContextWindowIndex'&keyword='jlca1003'"
        public static readonly int TOT_BITS = INT_BITS + PERIOD_BITS;

        //UPGRADE_NOTE: Final was removed from the declaration of 'INTERVAL '. "ms-help://MS.VSCC.v80/dv_commoner/local/redirect.htm?index='!DefaultContextWindowIndex'&keyword='jlca1003'"
        public static readonly int INTERVAL = 1 << INT_BITS;

        //UPGRADE_NOTE: Final was removed from the declaration of 'BIN_SCALE '. "ms-help://MS.VSCC.v80/dv_commoner/local/redirect.htm?index='!DefaultContextWindowIndex'&keyword='jlca1003'"
        public static readonly int BIN_SCALE = 1 << TOT_BITS;

        public const int MAX_FREQ = 124;

        private SEE2Context[][] SEE2Cont = new SEE2Context[25][];

        private SEE2Context dummySEE2Cont;

        private PPMContext minContext; //medContext

        private PPMContext maxContext;

        private State foundState; // found next state transition

        private int numMasked, initEsc, orderFall, maxOrder, runLength, initRL;

        private int[] charMask = new int[256];

        private int[] NS2Indx = new int[256];

        private int[] NS2BSIndx = new int[256];

        private int[] HB2Flag = new int[256];

        // byte EscCount, PrevSuccess, HiBitsFlag;
        private int escCount, prevSuccess, hiBitsFlag;

        private int[][] binSumm = new int[128][]; // binary SEE-contexts

        private RangeCoder coder;

        private SubAllocator subAlloc = new SubAllocator();

        private static int[] InitBinEsc = new int[] { 0x3CDD, 0x1F3F, 0x59BF, 0x48F3, 0x64A1, 0x5ABC, 0x6632, 0x6051 };

        // Temp fields
        //UPGRADE_NOTE: Final was removed from the declaration of 'tempState1 '. "ms-help://MS.VSCC.v80/dv_commoner/local/redirect.htm?index='!DefaultContextWindowIndex'&keyword='jlca1003'"
        private State tempState1 = new State(null);
        //UPGRADE_NOTE: Final was removed from the declaration of 'tempState2 '. "ms-help://MS.VSCC.v80/dv_commoner/local/redirect.htm?index='!DefaultContextWindowIndex'&keyword='jlca1003'"
        private State tempState2 = new State(null);
        //UPGRADE_NOTE: Final was removed from the declaration of 'tempState3 '. "ms-help://MS.VSCC.v80/dv_commoner/local/redirect.htm?index='!DefaultContextWindowIndex'&keyword='jlca1003'"
        private State tempState3 = new State(null);
        //UPGRADE_NOTE: Final was removed from the declaration of 'tempState4 '. "ms-help://MS.VSCC.v80/dv_commoner/local/redirect.htm?index='!DefaultContextWindowIndex'&keyword='jlca1003'"
        private State tempState4 = new State(null);
        //UPGRADE_NOTE: Final was removed from the declaration of 'tempStateRef1 '. "ms-help://MS.VSCC.v80/dv_commoner/local/redirect.htm?index='!DefaultContextWindowIndex'&keyword='jlca1003'"
        private StateRef tempStateRef1 = new StateRef();
        //UPGRADE_NOTE: Final was removed from the declaration of 'tempStateRef2 '. "ms-help://MS.VSCC.v80/dv_commoner/local/redirect.htm?index='!DefaultContextWindowIndex'&keyword='jlca1003'"
        private StateRef tempStateRef2 = new StateRef();
        //UPGRADE_NOTE: Final was removed from the declaration of 'tempPPMContext1 '. "ms-help://MS.VSCC.v80/dv_commoner/local/redirect.htm?index='!DefaultContextWindowIndex'&keyword='jlca1003'"
        private PPMContext tempPPMContext1 = new PPMContext(null);
        //UPGRADE_NOTE: Final was removed from the declaration of 'tempPPMContext2 '. "ms-help://MS.VSCC.v80/dv_commoner/local/redirect.htm?index='!DefaultContextWindowIndex'&keyword='jlca1003'"
        private PPMContext tempPPMContext2 = new PPMContext(null);
        //UPGRADE_NOTE: Final was removed from the declaration of 'tempPPMContext3 '. "ms-help://MS.VSCC.v80/dv_commoner/local/redirect.htm?index='!DefaultContextWindowIndex'&keyword='jlca1003'"
        private PPMContext tempPPMContext3 = new PPMContext(null);
        //UPGRADE_NOTE: Final was removed from the declaration of 'tempPPMContext4 '. "ms-help://MS.VSCC.v80/dv_commoner/local/redirect.htm?index='!DefaultContextWindowIndex'&keyword='jlca1003'"
        private PPMContext tempPPMContext4 = new PPMContext(null);
        //UPGRADE_NOTE: Final was removed from the declaration of 'ps '. "ms-help://MS.VSCC.v80/dv_commoner/local/redirect.htm?index='!DefaultContextWindowIndex'&keyword='jlca1003'"
        private int[] ps = new int[MAX_O];

        public ModelPPM()
        {
            InitBlock();
            minContext = null;
            maxContext = null;
            //medContext = null;
        }

        private void restartModelRare()
        {
            Utility.Fill(charMask, 0);
            subAlloc.initSubAllocator();
            initRL = -(maxOrder < 12 ? maxOrder : 12) - 1;
            int addr = subAlloc.allocContext();
            minContext.Address = addr;
            maxContext.Address = addr;
            minContext.setSuffix(0);
            orderFall = maxOrder;
            minContext.NumStats = 256;
            minContext.FreqData.SummFreq = minContext.NumStats + 1;

            addr = subAlloc.allocUnits(256 / 2);
            foundState.Address = addr;
            minContext.FreqData.SetStats(addr);

            State state = new State(subAlloc.Heap);
            addr = minContext.FreqData.GetStats();
            runLength = initRL;
            prevSuccess = 0;
            for (int i = 0; i < 256; i++)
            {
                state.Address = addr + i * State.Size;
                state.Symbol = i;
                state.Freq = 1;
                state.SetSuccessor(0);
            }

            for (int i = 0; i < 128; i++)
            {
                for (int k = 0; k < 8; k++)
                {
                    for (int m = 0; m < 64; m += 8)
                    {
                        binSumm[i][k + m] = BIN_SCALE - InitBinEsc[k] / (i + 2);
                    }
                }
            }
            for (int i = 0; i < 25; i++)
            {
                for (int k = 0; k < 16; k++)
                {
                    SEE2Cont[i][k].Initialize(5 * i + 10);
                }
            }
        }

        private void startModelRare(int MaxOrder)
        {
            int i, k, m, Step;
            escCount = 1;
            this.maxOrder = MaxOrder;
            restartModelRare();
            // Bug Fixed
            NS2BSIndx[0] = 0;
            NS2BSIndx[1] = 2;
            for (int j = 0; j < 9; j++)
            {
                NS2BSIndx[2 + j] = 4;
            }
            for (int j = 0; j < 256 - 11; j++)
            {
                NS2BSIndx[11 + j] = 6;
            }
            for (i = 0; i < 3; i++)
            {
                NS2Indx[i] = i;
            }
            for (m = i, k = 1, Step = 1; i < 256; i++)
            {
                NS2Indx[i] = m;
                if ((--k) == 0)
                {
                    k = ++Step;
                    m++;
                }
            }
            for (int j = 0; j < 0x40; j++)
            {
                HB2Flag[j] = 0;
            }
            for (int j = 0; j < 0x100 - 0x40; j++)
            {
                HB2Flag[0x40 + j] = 0x08;
            }
            dummySEE2Cont.Shift = PERIOD_BITS;
        }

        private void clearMask()
        {
            escCount = 1;
            Utility.Fill(charMask, 0);
        }

        internal bool decodeInit(Unpack unpackRead, int escChar)
        {

            int MaxOrder = unpackRead.Char & 0xff;
            bool reset = ((MaxOrder & 0x20) != 0);

            int MaxMB = 0;
            if (reset)
            {
                MaxMB = unpackRead.Char;
            }
            else
            {
                if (subAlloc.GetAllocatedMemory() == 0)
                {
                    return (false);
                }
            }
            if ((MaxOrder & 0x40) != 0)
            {
                escChar = unpackRead.Char;
                unpackRead.PpmEscChar = escChar;
            }
            coder = new RangeCoder(unpackRead);
            if (reset)
            {
                MaxOrder = (MaxOrder & 0x1f) + 1;
                if (MaxOrder > 16)
                {
                    MaxOrder = 16 + (MaxOrder - 16) * 3;
                }
                if (MaxOrder == 1)
                {
                    subAlloc.stopSubAllocator();
                    return (false);
                }
                subAlloc.startSubAllocator((MaxMB + 1) << 20);
                minContext = new PPMContext(Heap);
                //medContext = new PPMContext(Heap);
                maxContext = new PPMContext(Heap);
                foundState = new State(Heap);
                dummySEE2Cont = new SEE2Context();
                for (int i = 0; i < 25; i++)
                {
                    for (int j = 0; j < 16; j++)
                    {
                        SEE2Cont[i][j] = new SEE2Context();
                    }
                }
                startModelRare(MaxOrder);
            }
            return (minContext.Address != 0);
        }

        public virtual int decodeChar()
        {
            // Debug
            //subAlloc.dumpHeap();

            if (minContext.Address <= subAlloc.PText || minContext.Address > subAlloc.HeapEnd)
            {
                return (-1);
            }

            if (minContext.NumStats != 1)
            {
                if (minContext.FreqData.GetStats() <= subAlloc.PText || minContext.FreqData.GetStats() > subAlloc.HeapEnd)
                {
                    return (-1);
                }
                if (!minContext.decodeSymbol1(this))
                {
                    return (-1);
                }
            }
            else
            {
                minContext.decodeBinSymbol(this);
            }
            coder.Decode();
            while (foundState.Address == 0)
            {
                coder.AriDecNormalize();
                do
                {
                    orderFall++;
                    minContext.Address = minContext.getSuffix(); // =MinContext->Suffix;
                    if (minContext.Address <= subAlloc.PText || minContext.Address > subAlloc.HeapEnd)
                    {
                        return (-1);
                    }
                }
                while (minContext.NumStats == numMasked);
                if (!minContext.decodeSymbol2(this))
                {
                    return (-1);
                }
                coder.Decode();
            }
            int Symbol = foundState.Symbol;
            if ((orderFall == 0) && foundState.GetSuccessor() > subAlloc.PText)
            {
                // MinContext=MaxContext=FoundState->Successor;
                int addr = foundState.GetSuccessor();
                minContext.Address = addr;
                maxContext.Address = addr;
            }
            else
            {
                updateModel();
                //this.foundState.Address=foundState.Address);//TODO just 4 debugging
                if (escCount == 0)
                {
                    clearMask();
                }
            }
            coder.AriDecNormalize(); // ARI_DEC_NORMALIZE(Coder.code,Coder.low,Coder.range,Coder.UnpackRead);
            return (Symbol);
        }

        public virtual SEE2Context[][] getSEE2Cont()
        {
            return SEE2Cont;
        }

        public virtual void incEscCount(int dEscCount)
        {
            EscCount = EscCount + dEscCount;
        }

        public virtual void incRunLength(int dRunLength)
        {
            RunLength = RunLength + dRunLength;
        }

        public virtual int[] getHB2Flag()
        {
            return HB2Flag;
        }

        public virtual int[] getNS2BSIndx()
        {
            return NS2BSIndx;
        }

        public virtual int[] getNS2Indx()
        {
            return NS2Indx;
        }

        private int createSuccessors(bool Skip, State p1)
        {
            //State upState = tempState1.Initialize(null);
            StateRef upState = tempStateRef2;
            State tempState = tempState1.Initialize(Heap);

            // PPM_CONTEXT* pc=MinContext, * UpBranch=FoundState->Successor;
            PPMContext pc = tempPPMContext1.Initialize(Heap);
            pc.Address = minContext.Address;
            PPMContext upBranch = tempPPMContext2.Initialize(Heap);
            upBranch.Address = foundState.GetSuccessor();

            // STATE * p, * ps[MAX_O], ** pps=ps;
            State p = tempState2.Initialize(Heap);
            int pps = 0;

            bool noLoop = false;

            if (!Skip)
            {
                ps[pps++] = foundState.Address; // *pps++ = FoundState;
                if (pc.getSuffix() == 0)
                {
                    noLoop = true;
                }
            }
            if (!noLoop)
            {
                bool loopEntry = false;
                if (p1.Address != 0)
                {
                    p.Address = p1.Address;
                    pc.Address = pc.getSuffix(); // =pc->Suffix;
                    loopEntry = true;
                }
                do
                {
                    if (!loopEntry)
                    {
                        pc.Address = pc.getSuffix(); // pc=pc->Suffix;
                        if (pc.NumStats != 1)
                        {
                            p.Address = pc.FreqData.GetStats(); // p=pc->U.Stats
                            if (p.Symbol != foundState.Symbol)
                            {
                                do
                                {
                                    p.IncrementAddress();
                                }
                                while (p.Symbol != foundState.Symbol);
                            }
                        }
                        else
                        {
                            p.Address = pc.getOneState().Address; // p=&(pc->OneState);
                        }
                    } // LOOP_ENTRY:
                    loopEntry = false;
                    if (p.GetSuccessor() != upBranch.Address)
                    {
                        pc.Address = p.GetSuccessor(); // =p->Successor;
                        break;
                    }
                    ps[pps++] = p.Address;
                }
                while (pc.getSuffix() != 0);
            } // NO_LOOP:
            if (pps == 0)
            {
                return pc.Address;
            }
            upState.Symbol = Heap[upBranch.Address]; // UpState.Symbol=*(byte*)
            // UpBranch;
            // UpState.Successor=(PPM_CONTEXT*) (((byte*) UpBranch)+1);
            upState.SetSuccessor(upBranch.Address + 1); //TODO check if +1 necessary
            if (pc.NumStats != 1)
            {
                if (pc.Address <= subAlloc.PText)
                {
                    return (0);
                }
                p.Address = pc.FreqData.GetStats();
                if (p.Symbol != upState.Symbol)
                {
                    do
                    {
                        p.IncrementAddress();
                    }
                    while (p.Symbol != upState.Symbol);
                }
                int cf = p.Freq - 1;
                int s0 = pc.FreqData.SummFreq - pc.NumStats - cf;
                // UpState.Freq=1+((2*cf <= s0)?(5*cf > s0):((2*cf+3*s0-1)/(2*s0)));
                upState.Freq = 1 + ((2 * cf <= s0) ? (5 * cf > s0 ? 1 : 0) : ((2 * cf + 3 * s0 - 1) / (2 * s0)));
            }
            else
            {
                upState.Freq = pc.getOneState().Freq; // UpState.Freq=pc->OneState.Freq;
            }
            do
            {
                // pc = pc->createChild(this,*--pps,UpState);
                tempState.Address = ps[--pps];
                pc.Address = pc.createChild(this, tempState, upState);
                if (pc.Address == 0)
                {
                    return 0;
                }
            }
            while (pps != 0);
            return pc.Address;
        }

        private void updateModelRestart()
        {
            restartModelRare();
            escCount = 0;
        }

        private void updateModel()
        {
            //System.out.println("ModelPPM.updateModel()");
            // STATE fs = *FoundState, *p = NULL;
            StateRef fs = tempStateRef1;
            fs.Values = foundState;
            State p = tempState3.Initialize(Heap);
            State tempState = tempState4.Initialize(Heap);

            PPMContext pc = tempPPMContext3.Initialize(Heap);
            PPMContext successor = tempPPMContext4.Initialize(Heap);

            int ns1, ns, cf, sf, s0;
            pc.Address = minContext.getSuffix();
            if (fs.Freq < MAX_FREQ / 4 && pc.Address != 0)
            {
                if (pc.NumStats != 1)
                {
                    p.Address = pc.FreqData.GetStats();
                    if (p.Symbol != fs.Symbol)
                    {
                        do
                        {
                            p.IncrementAddress();
                        }
                        while (p.Symbol != fs.Symbol);
                        tempState.Address = p.Address - State.Size;
                        if (p.Freq >= tempState.Freq)
                        {
                            State.PPMDSwap(p, tempState);
                            p.DecrementAddress();
                        }
                    }
                    if (p.Freq < MAX_FREQ - 9)
                    {
                        p.IncrementFreq(2);
                        pc.FreqData.IncrementSummFreq(2);
                    }
                }
                else
                {
                    p.Address = pc.getOneState().Address;
                    if (p.Freq < 32)
                    {
                        p.IncrementFreq(1);
                    }
                }
            }
            if (orderFall == 0)
            {
                foundState.SetSuccessor(createSuccessors(true, p));
                minContext.Address = foundState.GetSuccessor();
                maxContext.Address = foundState.GetSuccessor();
                if (minContext.Address == 0)
                {
                    updateModelRestart();
                    return;
                }
                return;
            }
            subAlloc.Heap[subAlloc.PText] = (byte)fs.Symbol;
            subAlloc.incPText();
            successor.Address = subAlloc.PText;
            if (subAlloc.PText >= subAlloc.FakeUnitsStart)
            {
                updateModelRestart();
                return;
            }
            //        // Debug
            //        subAlloc.dumpHeap();
            if (fs.GetSuccessor() != 0)
            {
                if (fs.GetSuccessor() <= subAlloc.PText)
                {
                    fs.SetSuccessor(createSuccessors(false, p));
                    if (fs.GetSuccessor() == 0)
                    {
                        updateModelRestart();
                        return;
                    }
                }
                if (--orderFall == 0)
                {
                    successor.Address = fs.GetSuccessor();
                    if (maxContext.Address != minContext.Address)
                    {
                        subAlloc.decPText(1);
                    }
                }
            }
            else
            {
                foundState.SetSuccessor(successor.Address);
                fs.SetSuccessor(minContext);
            }
            //        // Debug
            //        subAlloc.dumpHeap();
            ns = minContext.NumStats;
            s0 = minContext.FreqData.SummFreq - (ns) - (fs.Freq - 1);
            for (pc.Address = maxContext.Address; pc.Address != minContext.Address; pc.Address = pc.getSuffix())
            {
                if ((ns1 = pc.NumStats) != 1)
                {
                    if ((ns1 & 1) == 0)
                    {
                        //System.out.println(ns1);
                        pc.FreqData.SetStats(subAlloc.expandUnits(pc.FreqData.GetStats(), Utility.URShift(ns1, 1)));
                        if (pc.FreqData.GetStats() == 0)
                        {
                            updateModelRestart();
                            return;
                        }
                    }
                    // bug fixed
                    //				int sum = ((2 * ns1 < ns) ? 1 : 0) +
                    //                        2 * ((4 * ((ns1 <= ns) ? 1 : 0)) & ((pc.getFreqData()
                    //								.getSummFreq() <= 8 * ns1) ? 1 : 0));
                    int sum = ((2 * ns1 < ns) ? 1 : 0) + 2 * (((4 * ns1 <= ns) ? 1 : 0) & ((pc.FreqData.SummFreq <= 8 * ns1) ? 1 : 0));
                    pc.FreqData.IncrementSummFreq(sum);
                }
                else
                {
                    p.Address = subAlloc.allocUnits(1);
                    if (p.Address == 0)
                    {
                        updateModelRestart();
                        return;
                    }
                    p.SetValues(pc.getOneState());
                    pc.FreqData.SetStats(p);
                    if (p.Freq < MAX_FREQ / 4 - 1)
                    {
                        p.IncrementFreq(p.Freq);
                    }
                    else
                    {
                        p.Freq = MAX_FREQ - 4;
                    }
                    pc.FreqData.SummFreq = (p.Freq + initEsc + (ns > 3 ? 1 : 0));
                }
                cf = 2 * fs.Freq * (pc.FreqData.SummFreq + 6);
                sf = s0 + pc.FreqData.SummFreq;
                if (cf < 6 * sf)
                {
                    cf = 1 + (cf > sf ? 1 : 0) + (cf >= 4 * sf ? 1 : 0);
                    pc.FreqData.IncrementSummFreq(3);
                }
                else
                {
                    cf = 4 + (cf >= 9 * sf ? 1 : 0) + (cf >= 12 * sf ? 1 : 0) + (cf >= 15 * sf ? 1 : 0);
                    pc.FreqData.IncrementSummFreq(cf);
                }
                p.Address = pc.FreqData.GetStats() + ns1 * State.Size;
                p.SetSuccessor(successor);
                p.Symbol = fs.Symbol;
                p.Freq = cf;
                pc.NumStats = ++ns1;
            }

            int address = fs.GetSuccessor();
            maxContext.Address = address;
            minContext.Address = address;
            //TODO-----debug
            //		int pos = minContext.getFreqData().getStats();
            //		State a = new State(getHeap());
            //		a.Address=pos);
            //		pos+=State.size;
            //		a.Address=pos);
            //--dbg end
            return;
        }

        // Debug
        public override System.String ToString()
        {
            StringBuilder buffer = new StringBuilder();
            buffer.Append("ModelPPM[");
            buffer.Append("\n  numMasked=");
            buffer.Append(numMasked);
            buffer.Append("\n  initEsc=");
            buffer.Append(initEsc);
            buffer.Append("\n  orderFall=");
            buffer.Append(orderFall);
            buffer.Append("\n  maxOrder=");
            buffer.Append(maxOrder);
            buffer.Append("\n  runLength=");
            buffer.Append(runLength);
            buffer.Append("\n  initRL=");
            buffer.Append(initRL);
            buffer.Append("\n  escCount=");
            buffer.Append(escCount);
            buffer.Append("\n  prevSuccess=");
            buffer.Append(prevSuccess);
            buffer.Append("\n  foundState=");
            buffer.Append(foundState);
            buffer.Append("\n  coder=");
            buffer.Append(coder);
            buffer.Append("\n  subAlloc=");
            buffer.Append(subAlloc);
            buffer.Append("\n]");
            return buffer.ToString();
        }

        // Debug
        //    public void dumpHeap() {
        //        subAlloc.dumpHeap();
        //    }

        internal bool decodeInit(Stream stream, int maxOrder, int maxMemory)
        {
            if (stream != null)
                coder = new RangeCoder(stream);

            if (maxOrder == 1)
            {
                subAlloc.stopSubAllocator();
                return (false);
            }
            subAlloc.startSubAllocator(maxMemory);
            minContext = new PPMContext(Heap);
            //medContext = new PPMContext(Heap);
            maxContext = new PPMContext(Heap);
            foundState = new State(Heap);
            dummySEE2Cont = new SEE2Context();
            for (int i = 0; i < 25; i++)
            {
                for (int j = 0; j < 16; j++)
                {
                    SEE2Cont[i][j] = new SEE2Context();
                }
            }
            startModelRare(maxOrder);

            return (minContext.Address != 0);
        }

        internal void nextContext()
        {
            int addr = foundState.GetSuccessor();
            if (orderFall == 0 && addr > subAlloc.PText)
            {
                minContext.Address = addr;
                maxContext.Address = addr;
            }
            else
                updateModel();
        }

        public int decodeChar(LZMA.RangeCoder.Decoder decoder)
        {
            if (minContext.NumStats != 1)
            {
                State s = tempState1.Initialize(Heap);
                s.Address = minContext.FreqData.GetStats();
                int i;
                int count, hiCnt;
                if ((count = (int)decoder.GetThreshold((uint)minContext.FreqData.SummFreq)) < (hiCnt = s.Freq))
                {
                    byte symbol;
                    decoder.Decode(0, (uint)s.Freq);
                    symbol = (byte)s.Symbol;
                    minContext.update1_0(this, s.Address);
                    nextContext();
                    return symbol;
                }
                prevSuccess = 0;
                i = minContext.NumStats - 1;
                do
                {
                    s.IncrementAddress();
                    if ((hiCnt += s.Freq) > count)
                    {
                        byte symbol;
                        decoder.Decode((uint)(hiCnt - s.Freq), (uint)s.Freq);
                        symbol = (byte)s.Symbol;
                        minContext.update1(this, s.Address);
                        nextContext();
                        return symbol;
                    }
                }
                while (--i > 0);
                if (count >= minContext.FreqData.SummFreq)
                    return -2;
                hiBitsFlag = HB2Flag[foundState.Symbol];
                decoder.Decode((uint)hiCnt, (uint)(minContext.FreqData.SummFreq - hiCnt));
                for (i = 0; i < 256; i++)
                    charMask[i] = -1;
                charMask[s.Symbol] = 0;
                i = minContext.NumStats - 1;
                do
                {
                    s.DecrementAddress();
                    charMask[s.Symbol] = 0;
                }
                while (--i > 0);
            }
            else
            {
                State rs = tempState1.Initialize(Heap);
                rs.Address = minContext.getOneState().Address;
                hiBitsFlag = getHB2Flag()[foundState.Symbol];
                int off1 = rs.Freq - 1;
                int off2 = minContext.getArrayIndex(this, rs);
                int bs = binSumm[off1][off2];
                if (decoder.DecodeBit((uint)bs, 14) == 0)
                {
                    byte symbol;
                    binSumm[off1][off2] = (bs + INTERVAL - minContext.getMean(bs, PERIOD_BITS, 2)) & 0xFFFF;
                    foundState.Address = rs.Address;
                    symbol = (byte)rs.Symbol;
                    rs.IncrementFreq((rs.Freq < 128) ? 1 : 0);
                    prevSuccess = 1;
                    incRunLength(1);
                    nextContext();
                    return symbol;
                }
                bs = (bs - minContext.getMean(bs, PERIOD_BITS, 2)) & 0xFFFF;
                binSumm[off1][off2] = bs;
                initEsc = PPMContext.ExpEscape[Utility.URShift(bs, 10)];
                int i;
                for (i = 0; i < 256; i++)
                    charMask[i] = -1;
                charMask[rs.Symbol] = 0;
                prevSuccess = 0;
            }
            for (;;)
            {
                State s = tempState1.Initialize(Heap);
                int i;
                int freqSum, count, hiCnt;
                SEE2Context see;
                int num, numMasked = minContext.NumStats;
                do
                {
                    orderFall++;
                    minContext.Address = minContext.getSuffix();
                    if (minContext.Address <= subAlloc.PText || minContext.Address > subAlloc.HeapEnd)
                        return -1;
                }
                while (minContext.NumStats == numMasked);
                hiCnt = 0;
                s.Address = minContext.FreqData.GetStats();
                i = 0;
                num = minContext.NumStats - numMasked;
                do
                {
                    int k = charMask[s.Symbol];
                    hiCnt += s.Freq & k;
                    minContext.ps[i] = s.Address;
                    s.IncrementAddress();
                    i -= k;
                }
                while (i != num);

                see = minContext.makeEscFreq(this, numMasked, out freqSum);
                freqSum += hiCnt;
                count = (int)decoder.GetThreshold((uint)freqSum);

                if (count < hiCnt)
                {
                    byte symbol;
                    State ps = tempState2.Initialize(Heap);
                    for (hiCnt = 0, i = 0, ps.Address = minContext.ps[i]; (hiCnt += ps.Freq) <= count; i++, ps.Address = minContext.ps[i]);
                    s.Address = ps.Address;
                    decoder.Decode((uint)(hiCnt - s.Freq), (uint)s.Freq);
                    see.update();
                    symbol = (byte)s.Symbol;
                    minContext.update2(this, s.Address);
                    updateModel();
                    return symbol;
                }
                if (count >= freqSum)
                    return -2;
                decoder.Decode((uint)hiCnt, (uint)(freqSum - hiCnt));
                see.Summ = see.Summ + freqSum;
                do
                {
                    s.Address = minContext.ps[--i];
                    charMask[s.Symbol] = 0;
                }
                while (i != 0);
            }
        }
    }
}