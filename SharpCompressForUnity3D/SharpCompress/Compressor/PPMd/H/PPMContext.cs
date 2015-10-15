namespace SharpCompress.Compressor.PPMd.H
{
    using SharpCompress;
    using System;
    using System.Runtime.InteropServices;
    using System.Text;

    internal class PPMContext : Pointer
    {
        public static readonly int[] ExpEscape = new int[] { 0x19, 14, 9, 7, 5, 5, 4, 4, 4, 3, 3, 3, 2, 2, 2, 2 };
        private SharpCompress.Compressor.PPMd.H.FreqData freqData;
        private int numStats;
        private SharpCompress.Compressor.PPMd.H.State oneState;
        internal int[] ps;
        public static readonly int size = ((2 + unionSize) + 4);
        private int suffix;
        private PPMContext tempPPMContext;
        private SharpCompress.Compressor.PPMd.H.State tempState1;
        private SharpCompress.Compressor.PPMd.H.State tempState2;
        private SharpCompress.Compressor.PPMd.H.State tempState3;
        private SharpCompress.Compressor.PPMd.H.State tempState4;
        private SharpCompress.Compressor.PPMd.H.State tempState5;
        private static readonly int unionSize = Math.Max(6, 6);

        public PPMContext(byte[] Memory) : base(Memory)
        {
            this.tempState1 = new SharpCompress.Compressor.PPMd.H.State(null);
            this.tempState2 = new SharpCompress.Compressor.PPMd.H.State(null);
            this.tempState3 = new SharpCompress.Compressor.PPMd.H.State(null);
            this.tempState4 = new SharpCompress.Compressor.PPMd.H.State(null);
            this.tempState5 = new SharpCompress.Compressor.PPMd.H.State(null);
            this.tempPPMContext = null;
            this.ps = new int[0x100];
            this.oneState = new SharpCompress.Compressor.PPMd.H.State(Memory);
            this.freqData = new SharpCompress.Compressor.PPMd.H.FreqData(Memory);
        }

        internal int createChild(ModelPPM model, SharpCompress.Compressor.PPMd.H.State pStats, StateRef firstState)
        {
            PPMContext successor = this.getTempPPMContext(model.SubAlloc.Heap);
            successor.Address = model.SubAlloc.allocContext();
            if (successor != null)
            {
                successor.NumStats = 1;
                successor.setOneState(firstState);
                successor.setSuffix(this);
                pStats.SetSuccessor(successor);
            }
            return successor.Address;
        }

        internal void decodeBinSymbol(ModelPPM model)
        {
            SharpCompress.Compressor.PPMd.H.State rs = this.tempState1.Initialize(model.Heap);
            rs.Address = this.oneState.Address;
            model.HiBitsFlag = model.getHB2Flag()[model.FoundState.Symbol];
            int index = rs.Freq - 1;
            int num2 = this.getArrayIndex(model, rs);
            int summ = model.BinSumm[index][num2];
            if (model.Coder.GetCurrentShiftCount(ModelPPM.TOT_BITS) < summ)
            {
                model.FoundState.Address = rs.Address;
                rs.IncrementFreq((rs.Freq < 0x80) ? 1 : 0);
                model.Coder.SubRange.LowCount = 0L;
                model.Coder.SubRange.HighCount = summ;
                summ = ((summ + ModelPPM.INTERVAL) - this.getMean(summ, 7, 2)) & 0xffff;
                model.BinSumm[index][num2] = summ;
                model.PrevSuccess = 1;
                model.incRunLength(1);
            }
            else
            {
                model.Coder.SubRange.LowCount = summ;
                summ = (summ - this.getMean(summ, 7, 2)) & 0xffff;
                model.BinSumm[index][num2] = summ;
                model.Coder.SubRange.HighCount = ModelPPM.BIN_SCALE;
                model.InitEsc = ExpEscape[Utility.URShift(summ, 10)];
                model.NumMasked = 1;
                model.CharMask[rs.Symbol] = model.EscCount;
                model.PrevSuccess = 0;
                model.FoundState.Address = 0;
            }
        }

        internal bool decodeSymbol1(ModelPPM model)
        {
            int num2;
            RangeCoder coder = model.Coder;
            coder.SubRange.Scale = this.freqData.SummFreq;
            SharpCompress.Compressor.PPMd.H.State state = new SharpCompress.Compressor.PPMd.H.State(model.Heap);
            state.Address = this.freqData.GetStats();
            long currentCount = coder.CurrentCount;
            if (currentCount >= coder.SubRange.Scale)
            {
                return false;
            }
            if (currentCount < (num2 = state.Freq))
            {
                coder.SubRange.HighCount = num2;
                model.PrevSuccess = ((2 * num2) > coder.SubRange.Scale) ? 1 : 0;
                model.incRunLength(model.PrevSuccess);
                num2 += 4;
                model.FoundState.Address = state.Address;
                model.FoundState.Freq = num2;
                this.freqData.IncrementSummFreq(4);
                if (num2 > 0x7c)
                {
                    this.rescale(model);
                }
                coder.SubRange.LowCount = 0L;
                return true;
            }
            if (model.FoundState.Address == 0)
            {
                return false;
            }
            model.PrevSuccess = 0;
            int numStats = this.NumStats;
            int num = numStats - 1;
            while ((num2 += state.IncrementAddress().Freq) <= currentCount)
            {
                if (--num == 0)
                {
                    model.HiBitsFlag = model.getHB2Flag()[model.FoundState.Symbol];
                    coder.SubRange.LowCount = num2;
                    model.CharMask[state.Symbol] = model.EscCount;
                    model.NumMasked = numStats;
                    num = numStats - 1;
                    model.FoundState.Address = 0;
                    do
                    {
                        model.CharMask[state.DecrementAddress().Symbol] = model.EscCount;
                    }
                    while (--num != 0);
                    coder.SubRange.HighCount = coder.SubRange.Scale;
                    return true;
                }
            }
            coder.SubRange.LowCount = num2 - state.Freq;
            coder.SubRange.HighCount = num2;
            this.update1(model, state.Address);
            return true;
        }

        internal bool decodeSymbol2(ModelPPM model)
        {
            int diff = this.NumStats - model.NumMasked;
            SEE2Context context = this.makeEscFreq2(model, diff);
            RangeCoder coder = model.Coder;
            SharpCompress.Compressor.PPMd.H.State state = this.tempState1.Initialize(model.Heap);
            SharpCompress.Compressor.PPMd.H.State state2 = this.tempState2.Initialize(model.Heap);
            state.Address = this.freqData.GetStats() - 6;
            int index = 0;
            int dScale = 0;
            do
            {
                do
                {
                    state.IncrementAddress();
                }
                while (model.CharMask[state.Symbol] == model.EscCount);
                dScale += state.Freq;
                this.ps[index++] = state.Address;
            }
            while (--diff != 0);
            coder.SubRange.incScale(dScale);
            long currentCount = coder.CurrentCount;
            if (currentCount >= coder.SubRange.Scale)
            {
                return false;
            }
            index = 0;
            state.Address = this.ps[index];
            if (currentCount < dScale)
            {
                dScale = 0;
                while ((dScale += state.Freq) <= currentCount)
                {
                    state.Address = this.ps[++index];
                }
                coder.SubRange.HighCount = dScale;
                coder.SubRange.LowCount = dScale - state.Freq;
                context.update();
                this.update2(model, state.Address);
            }
            else
            {
                coder.SubRange.LowCount = dScale;
                coder.SubRange.HighCount = coder.SubRange.Scale;
                diff = this.NumStats - model.NumMasked;
                index--;
                do
                {
                    state2.Address = this.ps[++index];
                    model.CharMask[state2.Symbol] = model.EscCount;
                }
                while (--diff != 0);
                context.incSumm((int) coder.SubRange.Scale);
                model.NumMasked = this.NumStats;
            }
            return true;
        }

        internal int getArrayIndex(ModelPPM Model, SharpCompress.Compressor.PPMd.H.State rs)
        {
            PPMContext context = this.getTempPPMContext(Model.SubAlloc.Heap);
            context.Address = this.getSuffix();
            int num = 0;
            num += Model.PrevSuccess;
            num += Model.getNS2BSIndx()[context.NumStats - 1];
            num += Model.HiBitsFlag + (2 * Model.getHB2Flag()[rs.Symbol]);
            return (num + (Utility.URShift(Model.RunLength, 0x1a) & 0x20));
        }

        internal int getMean(int summ, int shift, int round)
        {
            return Utility.URShift((int) (summ + (((int) 1) << (shift - round))), shift);
        }

        internal SharpCompress.Compressor.PPMd.H.State getOneState()
        {
            return this.oneState;
        }

        internal int getSuffix()
        {
            if (base.Memory != null)
            {
                this.suffix = Utility.readIntLittleEndian(base.Memory, this.Address + 8);
            }
            return this.suffix;
        }

        private PPMContext getTempPPMContext(byte[] Memory)
        {
            if (this.tempPPMContext == null)
            {
                this.tempPPMContext = new PPMContext(null);
            }
            return this.tempPPMContext.Initialize(Memory);
        }

        internal PPMContext Initialize(byte[] mem)
        {
            this.oneState.Initialize(mem);
            this.freqData.Initialize(mem);
            return base.Initialize<PPMContext>(mem);
        }

        internal SEE2Context makeEscFreq(ModelPPM model, int numMasked, out int escFreq)
        {
            SEE2Context context;
            int numStats = this.NumStats;
            int num2 = numStats - numMasked;
            if (numStats != 0x100)
            {
                PPMContext context2 = this.getTempPPMContext(model.Heap);
                context2.Address = this.getSuffix();
                int index = model.getNS2Indx()[num2 - 1];
                int num4 = 0;
                num4 += (num2 < (context2.NumStats - numStats)) ? 1 : 0;
                num4 += 2 * ((this.freqData.SummFreq < (11 * numStats)) ? 1 : 0);
                num4 += 4 * ((numMasked > num2) ? 1 : 0);
                num4 += model.HiBitsFlag;
                context = model.getSEE2Cont()[index][num4];
                escFreq = context.Mean;
                return context;
            }
            context = model.DummySEE2Cont;
            escFreq = 1;
            return context;
        }

        private SEE2Context makeEscFreq2(ModelPPM model, int Diff)
        {
            SEE2Context context;
            int numStats = this.NumStats;
            if (numStats != 0x100)
            {
                PPMContext context2 = this.getTempPPMContext(model.Heap);
                context2.Address = this.getSuffix();
                int index = model.getNS2Indx()[Diff - 1];
                int num3 = 0;
                num3 += (Diff < (context2.NumStats - numStats)) ? 1 : 0;
                num3 += 2 * ((this.freqData.SummFreq < (11 * numStats)) ? 1 : 0);
                num3 += 4 * ((model.NumMasked > Diff) ? 1 : 0);
                num3 += model.HiBitsFlag;
                context = model.getSEE2Cont()[index][num3];
                model.Coder.SubRange.Scale = context.Mean;
                return context;
            }
            context = model.DummySEE2Cont;
            model.Coder.SubRange.Scale = 1L;
            return context;
        }

        internal void rescale(ModelPPM model)
        {
            StateRef ref2;
            int numStats = this.NumStats;
            int num2 = this.NumStats - 1;
            SharpCompress.Compressor.PPMd.H.State state = new SharpCompress.Compressor.PPMd.H.State(model.Heap);
            SharpCompress.Compressor.PPMd.H.State state2 = new SharpCompress.Compressor.PPMd.H.State(model.Heap);
            SharpCompress.Compressor.PPMd.H.State state3 = new SharpCompress.Compressor.PPMd.H.State(model.Heap);
            state2.Address = model.FoundState.Address;
            while (state2.Address != this.freqData.GetStats())
            {
                state3.Address = state2.Address - 6;
                SharpCompress.Compressor.PPMd.H.State.PPMDSwap(state2, state3);
                state2.DecrementAddress();
            }
            state3.Address = this.freqData.GetStats();
            state3.IncrementFreq(4);
            this.freqData.IncrementSummFreq(4);
            int number = this.freqData.SummFreq - state2.Freq;
            int num3 = (model.OrderFall != 0) ? 1 : 0;
            state2.Freq = Utility.URShift((int) (state2.Freq + num3), 1);
            this.freqData.SummFreq = state2.Freq;
            do
            {
                state2.IncrementAddress();
                number -= state2.Freq;
                state2.Freq = Utility.URShift((int) (state2.Freq + num3), 1);
                this.freqData.IncrementSummFreq(state2.Freq);
                state3.Address = state2.Address - 6;
                if (state2.Freq > state3.Freq)
                {
                    state.Address = state2.Address;
                    ref2 = new StateRef();
                    ref2.Values = state;
                    SharpCompress.Compressor.PPMd.H.State ptr = new SharpCompress.Compressor.PPMd.H.State(model.Heap);
                    SharpCompress.Compressor.PPMd.H.State state5 = new SharpCompress.Compressor.PPMd.H.State(model.Heap);
                    do
                    {
                        ptr.Address = state.Address - 6;
                        state.SetValues(ptr);
                        state.DecrementAddress();
                        state5.Address = state.Address - 6;
                    }
                    while ((state.Address != this.freqData.GetStats()) && (ref2.Freq > state5.Freq));
                    state.SetValues(ref2);
                }
            }
            while (--num2 != 0);
            if (state2.Freq == 0)
            {
                do
                {
                    num2++;
                    state2.DecrementAddress();
                }
                while (state2.Freq == 0);
                number += num2;
                this.NumStats -= num2;
                if (this.NumStats == 1)
                {
                    ref2 = new StateRef();
                    state3.Address = this.freqData.GetStats();
                    ref2.Values = state3;
                    do
                    {
                        ref2.DecrementFreq(Utility.URShift(ref2.Freq, 1));
                        number = Utility.URShift(number, 1);
                    }
                    while (number > 1);
                    model.SubAlloc.freeUnits(this.freqData.GetStats(), Utility.URShift((int) (numStats + 1), 1));
                    this.oneState.SetValues(ref2);
                    model.FoundState.Address = this.oneState.Address;
                    return;
                }
            }
            number -= Utility.URShift(number, 1);
            this.freqData.IncrementSummFreq(number);
            int oldNU = Utility.URShift((int) (numStats + 1), 1);
            int newNU = Utility.URShift((int) (this.NumStats + 1), 1);
            if (oldNU != newNU)
            {
                this.freqData.SetStats(model.SubAlloc.shrinkUnits(this.freqData.GetStats(), oldNU, newNU));
            }
            model.FoundState.Address = this.freqData.GetStats();
        }

        internal void setOneState(StateRef oneState)
        {
            this.oneState.SetValues(oneState);
        }

        internal void setSuffix(PPMContext suffix)
        {
            this.setSuffix(suffix.Address);
        }

        internal void setSuffix(int suffix)
        {
            this.suffix = suffix;
            if (base.Memory != null)
            {
                Utility.WriteLittleEndian(base.Memory, this.Address + 8, suffix);
            }
        }

        public override string ToString()
        {
            StringBuilder builder = new StringBuilder();
            builder.Append("PPMContext[");
            builder.Append("\n  Address=");
            builder.Append(this.Address);
            builder.Append("\n  size=");
            builder.Append(size);
            builder.Append("\n  numStats=");
            builder.Append(this.NumStats);
            builder.Append("\n  Suffix=");
            builder.Append(this.getSuffix());
            builder.Append("\n  freqData=");
            builder.Append(this.freqData);
            builder.Append("\n  oneState=");
            builder.Append(this.oneState);
            builder.Append("\n]");
            return builder.ToString();
        }

        internal void update1(ModelPPM model, int p)
        {
            model.FoundState.Address = p;
            model.FoundState.IncrementFreq(4);
            this.freqData.IncrementSummFreq(4);
            SharpCompress.Compressor.PPMd.H.State state = this.tempState3.Initialize(model.Heap);
            SharpCompress.Compressor.PPMd.H.State state2 = this.tempState4.Initialize(model.Heap);
            state.Address = p;
            state2.Address = p - 6;
            if (state.Freq > state2.Freq)
            {
                SharpCompress.Compressor.PPMd.H.State.PPMDSwap(state, state2);
                model.FoundState.Address = state2.Address;
                if (state2.Freq > 0x7c)
                {
                    this.rescale(model);
                }
            }
        }

        internal void update1_0(ModelPPM model, int p)
        {
            model.FoundState.Address = p;
            model.PrevSuccess = ((2 * model.FoundState.Freq) > this.freqData.SummFreq) ? 1 : 0;
            model.incRunLength(model.PrevSuccess);
            this.freqData.IncrementSummFreq(4);
            model.FoundState.IncrementFreq(4);
            if (model.FoundState.Freq > 0x7c)
            {
                this.rescale(model);
            }
        }

        internal void update2(ModelPPM model, int p)
        {
            SharpCompress.Compressor.PPMd.H.State state = this.tempState5.Initialize(model.Heap);
            state.Address = p;
            model.FoundState.Address = p;
            model.FoundState.IncrementFreq(4);
            this.freqData.IncrementSummFreq(4);
            if (state.Freq > 0x7c)
            {
                this.rescale(model);
            }
            model.incEscCount(1);
            model.RunLength = model.InitRL;
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
                this.oneState.Address = value + 2;
                this.freqData.Address = value + 2;
            }
        }

        internal SharpCompress.Compressor.PPMd.H.FreqData FreqData
        {
            get
            {
                return this.freqData;
            }
            set
            {
                this.freqData.SummFreq = value.SummFreq;
                this.freqData.SetStats(value.GetStats());
            }
        }

        public virtual int NumStats
        {
            get
            {
                if (base.Memory != null)
                {
                    this.numStats = Utility.readShortLittleEndian(base.Memory, this.Address) & 0xffff;
                }
                return this.numStats;
            }
            set
            {
                this.numStats = value & 0xffff;
                if (base.Memory != null)
                {
                    Utility.WriteLittleEndian(base.Memory, this.Address, (short) value);
                }
            }
        }
    }
}

