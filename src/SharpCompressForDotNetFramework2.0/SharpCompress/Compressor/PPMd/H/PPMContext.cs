using System.Text;
namespace SharpCompress.Compressor.PPMd.H
{
    internal class PPMContext : Pointer
    {
        internal FreqData FreqData
        {
            get
            {
                return freqData;
            }

            set
            {
                this.freqData.SummFreq = value.SummFreq;
                this.freqData.SetStats(value.GetStats());
            }

        }
        virtual public int NumStats
        {
            get
            {
                if (Memory != null)
                {
                    numStats = Utility.readShortLittleEndian(Memory, Address) & 0xffff;
                }
                return numStats;
            }

            set
            {
                this.numStats = value & 0xffff;
                if (Memory != null)
                {
                    Utility.WriteLittleEndian(Memory, Address, (short)value);
                }
            }

        }

        //UPGRADE_NOTE: Final was removed from the declaration of 'unionSize '. "ms-help://MS.VSCC.v80/dv_commoner/local/redirect.htm?index='!DefaultContextWindowIndex'&keyword='jlca1003'"
        //UPGRADE_NOTE: The initialization of  'unionSize' was moved to static method 'SharpCompress.Unpack.PPM.PPMContext'. "ms-help://MS.VSCC.v80/dv_commoner/local/redirect.htm?index='!DefaultContextWindowIndex'&keyword='jlca1005'"
        private static readonly int unionSize;

        //UPGRADE_NOTE: Final was removed from the declaration of 'size '. "ms-help://MS.VSCC.v80/dv_commoner/local/redirect.htm?index='!DefaultContextWindowIndex'&keyword='jlca1003'"
        public static readonly int size = 2 + unionSize + 4; // 12

        // ushort NumStats;
        private int numStats; // determines if feqData or onstate is used

        // (1==onestate)

        //UPGRADE_NOTE: Final was removed from the declaration of 'freqData '. "ms-help://MS.VSCC.v80/dv_commoner/local/redirect.htm?index='!DefaultContextWindowIndex'&keyword='jlca1003'"
        private FreqData freqData; // -\

        // |-> union
        //UPGRADE_NOTE: Final was removed from the declaration of 'oneState '. "ms-help://MS.VSCC.v80/dv_commoner/local/redirect.htm?index='!DefaultContextWindowIndex'&keyword='jlca1003'"
        private State oneState; // -/

        private int suffix; // pointer ppmcontext

        //UPGRADE_NOTE: Final was removed from the declaration of 'ExpEscape'. "ms-help://MS.VSCC.v80/dv_commoner/local/redirect.htm?index='!DefaultContextWindowIndex'&keyword='jlca1003'"
        public static readonly int[] ExpEscape = new int[] { 25, 14, 9, 7, 5, 5, 4, 4, 4, 3, 3, 3, 2, 2, 2, 2 };

        // Temp fields
        //UPGRADE_NOTE: Final was removed from the declaration of 'tempState1 '. "ms-help://MS.VSCC.v80/dv_commoner/local/redirect.htm?index='!DefaultContextWindowIndex'&keyword='jlca1003'"
        private State tempState1 = new State(null);
        //UPGRADE_NOTE: Final was removed from the declaration of 'tempState2 '. "ms-help://MS.VSCC.v80/dv_commoner/local/redirect.htm?index='!DefaultContextWindowIndex'&keyword='jlca1003'"
        private State tempState2 = new State(null);
        //UPGRADE_NOTE: Final was removed from the declaration of 'tempState3 '. "ms-help://MS.VSCC.v80/dv_commoner/local/redirect.htm?index='!DefaultContextWindowIndex'&keyword='jlca1003'"
        private State tempState3 = new State(null);
        //UPGRADE_NOTE: Final was removed from the declaration of 'tempState4 '. "ms-help://MS.VSCC.v80/dv_commoner/local/redirect.htm?index='!DefaultContextWindowIndex'&keyword='jlca1003'"
        private State tempState4 = new State(null);
        //UPGRADE_NOTE: Final was removed from the declaration of 'tempState5 '. "ms-help://MS.VSCC.v80/dv_commoner/local/redirect.htm?index='!DefaultContextWindowIndex'&keyword='jlca1003'"
        private State tempState5 = new State(null);
        private PPMContext tempPPMContext = null;
        //UPGRADE_NOTE: Final was removed from the declaration of 'ps '. "ms-help://MS.VSCC.v80/dv_commoner/local/redirect.htm?index='!DefaultContextWindowIndex'&keyword='jlca1003'"
        internal int[] ps = new int[256];

        public PPMContext(byte[] Memory)
            : base(Memory)
        {
            oneState = new State(Memory);
            freqData = new FreqData(Memory);
        }

        internal PPMContext Initialize(byte[] mem)
        {
            oneState.Initialize(mem);
            freqData.Initialize(mem);
            return base.Initialize<PPMContext>(mem);
        }

        internal State getOneState()
        {
            return oneState;
        }

        internal void setOneState(StateRef oneState)
        {
            this.oneState.SetValues(oneState);
        }

        internal int getSuffix()
        {
            if (Memory != null)
            {
                suffix = Utility.readIntLittleEndian(Memory, Address + 8);
            }
            return suffix;
        }

        internal void setSuffix(PPMContext suffix)
        {
            setSuffix(suffix.Address);
        }

        internal void setSuffix(int suffix)
        {
            this.suffix = suffix;
            if (Memory != null)
            {
                Utility.WriteLittleEndian(Memory, Address + 8, suffix);
            }
        }

        internal override int Address
        {
            get
            {
                return base.Address;
            }
            set
            {
                base.Address = value;
                oneState.Address = value + 2;
                freqData.Address = value + 2;
            }
        }

        private PPMContext getTempPPMContext(byte[] Memory)
        {
            if (tempPPMContext == null)
            {
                tempPPMContext = new PPMContext(null);
            }
            return tempPPMContext.Initialize(Memory);
        }

        internal int createChild(ModelPPM model, State pStats, StateRef firstState)
        {
            PPMContext pc = getTempPPMContext(model.SubAlloc.Heap);
            pc.Address = model.SubAlloc.allocContext();
            if (pc != null)
            {
                pc.NumStats = 1;
                pc.setOneState(firstState);
                pc.setSuffix(this);
                pStats.SetSuccessor(pc);
            }
            return pc.Address;
        }

        internal void rescale(ModelPPM model)
        {
            int OldNS = NumStats, i = NumStats - 1, Adder, EscFreq;
            // STATE* p1, * p;
            State p1 = new State(model.Heap);
            State p = new State(model.Heap);
            State temp = new State(model.Heap);

            for (p.Address = model.FoundState.Address; p.Address != freqData.GetStats(); p.DecrementAddress())
            {
                temp.Address = p.Address - State.Size;
                State.PPMDSwap(p, temp);
            }
            temp.Address = freqData.GetStats();
            temp.IncrementFreq(4);
            freqData.IncrementSummFreq(4);
            EscFreq = freqData.SummFreq - p.Freq;
            Adder = (model.OrderFall != 0) ? 1 : 0;
            p.Freq = Utility.URShift((p.Freq + Adder), 1);
            freqData.SummFreq = p.Freq;
            do
            {
                p.IncrementAddress();
                EscFreq -= p.Freq;
                p.Freq = Utility.URShift((p.Freq + Adder), 1);
                freqData.IncrementSummFreq(p.Freq);
                temp.Address = p.Address - State.Size;
                if (p.Freq > temp.Freq)
                {
                    p1.Address = p.Address;
                    StateRef tmp = new StateRef();
                    tmp.Values = p1;
                    State temp2 = new State(model.Heap);
                    State temp3 = new State(model.Heap);
                    do
                    {
                        // p1[0]=p1[-1];
                        temp2.Address = p1.Address - State.Size;
                        p1.SetValues(temp2);
                        p1.DecrementAddress();
                        temp3.Address = p1.Address - State.Size;
                    }
                    while (p1.Address != freqData.GetStats() && tmp.Freq > temp3.Freq);
                    p1.SetValues(tmp);
                }
            }
            while (--i != 0);
            if (p.Freq == 0)
            {
                do
                {
                    i++;
                    p.DecrementAddress();
                }
                while (p.Freq == 0);
                EscFreq += i;
                NumStats = NumStats - i;
                if (NumStats == 1)
                {
                    StateRef tmp = new StateRef();
                    temp.Address = freqData.GetStats();
                    tmp.Values = temp;
                    // STATE tmp=*U.Stats;
                    do
                    {
                        // tmp.Freq-=(tmp.Freq >> 1)
                        tmp.DecrementFreq(Utility.URShift(tmp.Freq, 1));
                        EscFreq = Utility.URShift(EscFreq, 1);
                    }
                    while (EscFreq > 1);
                    model.SubAlloc.freeUnits(freqData.GetStats(), Utility.URShift((OldNS + 1), 1));
                    oneState.SetValues(tmp);
                    model.FoundState.Address = oneState.Address;
                    return;
                }
            }
            EscFreq -= Utility.URShift(EscFreq, 1);
            freqData.IncrementSummFreq(EscFreq);
            int n0 = Utility.URShift((OldNS + 1), 1), n1 = Utility.URShift((NumStats + 1), 1);
            if (n0 != n1)
            {
                freqData.SetStats(model.SubAlloc.shrinkUnits(freqData.GetStats(), n0, n1));
            }
            model.FoundState.Address = freqData.GetStats();
        }

        internal int getArrayIndex(ModelPPM Model, State rs)
        {
            PPMContext tempSuffix = getTempPPMContext(Model.SubAlloc.Heap);
            tempSuffix.Address = getSuffix();
            int ret = 0;
            ret += Model.PrevSuccess;
            ret += Model.getNS2BSIndx()[tempSuffix.NumStats - 1];
            ret += Model.HiBitsFlag + 2 * Model.getHB2Flag()[rs.Symbol];
            ret += ((Utility.URShift(Model.RunLength, 26)) & 0x20);
            return ret;
        }

        internal int getMean(int summ, int shift, int round)
        {
            return (Utility.URShift((summ + (1 << (shift - round))), (shift)));
        }

        internal void decodeBinSymbol(ModelPPM model)
        {
            State rs = tempState1.Initialize(model.Heap);
            rs.Address = oneState.Address; // State&
            model.HiBitsFlag = model.getHB2Flag()[model.FoundState.Symbol];
            int off1 = rs.Freq - 1;
            int off2 = getArrayIndex(model, rs);
            int bs = model.BinSumm[off1][off2];
            if (model.Coder.GetCurrentShiftCount(ModelPPM.TOT_BITS) < bs)
            {
                model.FoundState.Address = rs.Address;
                rs.IncrementFreq((rs.Freq < 128) ? 1 : 0);
                model.Coder.SubRange.LowCount = 0;
                model.Coder.SubRange.HighCount = bs;
                bs = ((bs + ModelPPM.INTERVAL - getMean(bs, ModelPPM.PERIOD_BITS, 2)) & 0xffff);
                model.BinSumm[off1][off2] = bs;
                model.PrevSuccess = 1;
                model.incRunLength(1);
            }
            else
            {
                model.Coder.SubRange.LowCount = bs;
                bs = (bs - getMean(bs, ModelPPM.PERIOD_BITS, 2)) & 0xFFFF;
                model.BinSumm[off1][off2] = bs;
                model.Coder.SubRange.HighCount = ModelPPM.BIN_SCALE;
                model.InitEsc = ExpEscape[Utility.URShift(bs, 10)];
                model.NumMasked = 1;
                model.CharMask[rs.Symbol] = model.EscCount;
                model.PrevSuccess = 0;
                model.FoundState.Address = 0;
            }
            //int a = 0;//TODO just 4 debugging
        }

        //	public static void ppmdSwap(ModelPPM model, StatePtr state1, StatePtr state2)
        //	{
        //		byte[] bytes = model.getSubAlloc().getHeap();
        //		int p1 = state1.Address;
        //		int p2 = state2.Address;
        //		
        //		for (int i = 0; i < StatePtr.size; i++) {
        //			byte temp = bytes[p1+i];
        //			bytes[p1+i] = bytes[p2+i];
        //			bytes[p2+i] = temp;
        //		}
        //		state1.Address=p1);
        //		state2.Address=p2);
        //	}

        internal void update1(ModelPPM model, int p)
        {
            model.FoundState.Address = p;
            model.FoundState.IncrementFreq(4);
            freqData.IncrementSummFreq(4);
            State p0 = tempState3.Initialize(model.Heap);
            State p1 = tempState4.Initialize(model.Heap);
            p0.Address = p;
            p1.Address = p - State.Size;
            if (p0.Freq > p1.Freq)
            {
                State.PPMDSwap(p0, p1);
                model.FoundState.Address = p1.Address;
                if (p1.Freq > ModelPPM.MAX_FREQ)
                    rescale(model);
            }
        }

        internal void update1_0(ModelPPM model, int p)
        {
            model.FoundState.Address = p;
            model.PrevSuccess = 2 * model.FoundState.Freq > freqData.SummFreq ? 1 : 0;
            model.incRunLength(model.PrevSuccess);
            freqData.IncrementSummFreq(4);
            model.FoundState.IncrementFreq(4);
            if (model.FoundState.Freq > ModelPPM.MAX_FREQ)
                rescale(model);
        }

        internal bool decodeSymbol2(ModelPPM model)
        {
            long count;
            int hiCnt, i = NumStats - model.NumMasked;
            SEE2Context psee2c = makeEscFreq2(model, i);
            RangeCoder coder = model.Coder;
            // STATE* ps[256], ** pps=ps, * p=U.Stats-1;
            State p = tempState1.Initialize(model.Heap);
            State temp = tempState2.Initialize(model.Heap);
            p.Address = freqData.GetStats() - State.Size;
            int pps = 0;
            hiCnt = 0;

            do
            {
                do
                {
                    p.IncrementAddress(); // p++;
                }
                while (model.CharMask[p.Symbol] == model.EscCount);
                hiCnt += p.Freq;
                ps[pps++] = p.Address;
            }
            while (--i != 0);
            coder.SubRange.incScale(hiCnt);
            count = coder.CurrentCount;
            if (count >= coder.SubRange.Scale)
            {
                return false;
            }
            pps = 0;
            p.Address = ps[pps];
            if (count < hiCnt)
            {
                hiCnt = 0;
                while ((hiCnt += p.Freq) <= count)
                {
                    p.Address = ps[++pps]; // p=*++pps;
                }
                coder.SubRange.HighCount = hiCnt;
                coder.SubRange.LowCount = hiCnt - p.Freq;
                psee2c.update();
                update2(model, p.Address);
            }
            else
            {
                coder.SubRange.LowCount = hiCnt;
                coder.SubRange.HighCount = coder.SubRange.Scale;
                i = NumStats - model.NumMasked; // ->NumMasked;
                pps--;
                do
                {
                    temp.Address = ps[++pps]; // (*++pps)
                    model.CharMask[temp.Symbol] = model.EscCount;
                }
                while (--i != 0);
                psee2c.incSumm((int)coder.SubRange.Scale);
                model.NumMasked = NumStats;
            }
            return (true);
        }

        internal void update2(ModelPPM model, int p)
        {
            State temp = tempState5.Initialize(model.Heap);
            temp.Address = p;
            model.FoundState.Address = p;
            model.FoundState.IncrementFreq(4);
            freqData.IncrementSummFreq(4);
            if (temp.Freq > ModelPPM.MAX_FREQ)
            {
                rescale(model);
            }
            model.incEscCount(1);
            model.RunLength = model.InitRL;
        }

        private SEE2Context makeEscFreq2(ModelPPM model, int Diff)
        {
            SEE2Context psee2c;
            int numStats = NumStats;
            if (numStats != 256)
            {
                PPMContext suff = getTempPPMContext(model.Heap);
                suff.Address = getSuffix();
                int idx1 = model.getNS2Indx()[Diff - 1];
                int idx2 = 0;
                idx2 += ((Diff < suff.NumStats - numStats) ? 1 : 0);
                idx2 += 2 * ((freqData.SummFreq < 11 * numStats) ? 1 : 0);
                idx2 += 4 * ((model.NumMasked > Diff) ? 1 : 0);
                idx2 += model.HiBitsFlag;
                psee2c = model.getSEE2Cont()[idx1][idx2];
                model.Coder.SubRange.Scale = psee2c.Mean;
            }
            else
            {
                psee2c = model.DummySEE2Cont;
                model.Coder.SubRange.Scale = 1;
            }
            return psee2c;
        }

        internal SEE2Context makeEscFreq(ModelPPM model, int numMasked, out int escFreq)
        {
            SEE2Context psee2c;
            int numStats = NumStats;
            int nonMasked = numStats - numMasked;
            if (numStats != 256)
            {
                PPMContext suff = getTempPPMContext(model.Heap);
                suff.Address = getSuffix();
                int idx1 = model.getNS2Indx()[nonMasked - 1];
                int idx2 = 0;
                idx2 += ((nonMasked < suff.NumStats - numStats) ? 1 : 0);
                idx2 += 2 * ((freqData.SummFreq < 11 * numStats) ? 1 : 0);
                idx2 += 4 * ((numMasked > nonMasked) ? 1 : 0);
                idx2 += model.HiBitsFlag;
                psee2c = model.getSEE2Cont()[idx1][idx2];
                escFreq = psee2c.Mean;
            }
            else
            {
                psee2c = model.DummySEE2Cont;
                escFreq = 1;
            }
            return psee2c;
        }

        internal bool decodeSymbol1(ModelPPM model)
        {

            RangeCoder coder = model.Coder;
            coder.SubRange.Scale = freqData.SummFreq;
            State p = new State(model.Heap);
            p.Address = freqData.GetStats();
            int i, HiCnt;
            long count = coder.CurrentCount;
            if (count >= coder.SubRange.Scale)
            {
                return false;
            }
            if (count < (HiCnt = p.Freq))
            {
                coder.SubRange.HighCount = HiCnt;
                model.PrevSuccess = (2 * HiCnt > coder.SubRange.Scale) ? 1 : 0;
                model.incRunLength(model.PrevSuccess);
                HiCnt += 4;
                model.FoundState.Address = p.Address;
                model.FoundState.Freq = HiCnt;
                freqData.IncrementSummFreq(4);
                if (HiCnt > ModelPPM.MAX_FREQ)
                {
                    rescale(model);
                }
                coder.SubRange.LowCount = 0;
                return true;
            }
            else
            {
                if (model.FoundState.Address == 0)
                {
                    return (false);
                }
            }
            model.PrevSuccess = 0;
            int numStats = NumStats;
            i = numStats - 1;
            while ((HiCnt += p.IncrementAddress().Freq) <= count)
            {
                if (--i == 0)
                {
                    model.HiBitsFlag = model.getHB2Flag()[model.FoundState.Symbol];
                    coder.SubRange.LowCount = HiCnt;
                    model.CharMask[p.Symbol] = model.EscCount;
                    model.NumMasked = numStats;
                    i = numStats - 1;
                    model.FoundState.Address = 0;
                    do
                    {
                        model.CharMask[p.DecrementAddress().Symbol] = model.EscCount;
                    }
                    while (--i != 0);
                    coder.SubRange.HighCount = coder.SubRange.Scale;
                    return (true);
                }
            }
            coder.SubRange.LowCount = HiCnt - p.Freq;
            coder.SubRange.HighCount = HiCnt;
            update1(model, p.Address);
            return (true);
        }

        public override System.String ToString()
        {
            StringBuilder buffer = new StringBuilder();
            buffer.Append("PPMContext[");
            buffer.Append("\n  Address=");
            buffer.Append(Address);
            buffer.Append("\n  size=");
            buffer.Append(size);
            buffer.Append("\n  numStats=");
            buffer.Append(NumStats);
            buffer.Append("\n  Suffix=");
            buffer.Append(getSuffix());
            buffer.Append("\n  freqData=");
            buffer.Append(freqData);
            buffer.Append("\n  oneState=");
            buffer.Append(oneState);
            buffer.Append("\n]");
            return buffer.ToString();
        }
        static PPMContext()
        {
            unionSize = System.Math.Max(FreqData.Size, State.Size);
        }
    }
}