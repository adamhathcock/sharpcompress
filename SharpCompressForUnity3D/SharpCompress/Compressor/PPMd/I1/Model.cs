namespace SharpCompress.Compressor.PPMd.I1
{
    using SharpCompress.Compressor.PPMd;
    using System;
    using System.IO;
    using System.Runtime.InteropServices;

    internal class Model
    {
        private SharpCompress.Compressor.PPMd.I1.Allocator Allocator;
        private const uint BinaryScale = 0x4000;
        private ushort[,] binarySummary;
        private byte[] characterMask;
        private SharpCompress.Compressor.PPMd.I1.Coder Coder;
        private PpmState[] decodeStates;
        private See2Context emptySee2Context;
        private byte escapeCount;
        private static readonly byte[] ExponentialEscapes = new byte[] { 0x19, 14, 9, 7, 5, 5, 4, 4, 4, 3, 3, 3, 2, 2, 2, 2 };
        private PpmState foundState;
        private static readonly ushort[] InitialBinaryEscapes = new ushort[] { 0x3cdd, 0x1f3f, 0x59bf, 0x48f3, 0x64a1, 0x5abc, 0x6632, 0x6051 };
        private int initialEscape;
        private int initialRunLength;
        private const uint Interval = 0x80;
        private const byte IntervalBitCount = 7;
        private PpmContext maximumContext;
        private const uint MaximumFrequency = 0x7c;
        public const int MaximumOrder = 0x10;
        private ModelRestorationMethod method;
        private PpmContext minimumContext;
        private int modelOrder;
        private byte numberMasked;
        private byte numberStatistics;
        private byte[] numberStatisticsToBinarySummaryIndex;
        private const uint OrderBound = 9;
        private int orderFall;
        private const byte PeriodBitCount = 7;
        private byte previousSuccess;
        private byte[] probabilities;
        private int runLength;
        private See2Context[,] see2Contexts;
        public const uint Signature = 0x84acaf8f;
        private const byte TotalBitCount = 14;
        private const byte UpperFrequency = 5;
        public const char Variant = 'I';

        public Model()
        {
            int num;
            this.binarySummary = new ushort[0x19, 0x40];
            this.numberStatisticsToBinarySummaryIndex = new byte[0x100];
            this.probabilities = new byte[260];
            this.characterMask = new byte[0x100];
            this.decodeStates = new PpmState[0x100];
            this.numberStatisticsToBinarySummaryIndex[0] = 0;
            this.numberStatisticsToBinarySummaryIndex[1] = 2;
            for (num = 2; num < 11; num++)
            {
                this.numberStatisticsToBinarySummaryIndex[num] = 4;
            }
            for (num = 11; num < 0x100; num++)
            {
                this.numberStatisticsToBinarySummaryIndex[num] = 6;
            }
            uint num2 = 1;
            uint num3 = 1;
            uint num4 = 5;
            for (num = 0; num < 5; num++)
            {
                this.probabilities[num] = (byte) num;
            }
            for (num = 5; num < 260; num++)
            {
                this.probabilities[num] = (byte) num4;
                num2--;
                if (num2 == 0)
                {
                    num3++;
                    num2 = num3;
                    num4++;
                }
            }
            this.see2Contexts = new See2Context[0x18, 0x20];
            for (int i = 0; i < 0x18; i++)
            {
                for (int j = 0; j < 0x20; j++)
                {
                    this.see2Contexts[i, j] = new See2Context();
                }
            }
            this.emptySee2Context = new See2Context();
            this.emptySee2Context.Summary = 0xaf8f;
            this.emptySee2Context.Shift = 0xac;
            this.emptySee2Context.Count = 0x84;
        }

        private void ClearMask()
        {
            this.escapeCount = 1;
            Array.Clear(this.characterMask, 0, this.characterMask.Length);
        }

        private static void Copy(PpmState state1, PpmState state2)
        {
            state1.Symbol = state2.Symbol;
            state1.Frequency = state2.Frequency;
            state1.Successor = state2.Successor;
        }

        private PpmContext CreateSuccessors(bool skip, PpmState state, PpmContext context)
        {
            byte num3;
            PpmState state2;
            PpmContext successor = this.foundState.Successor;
            PpmState[] stateArray = new PpmState[0x10];
            uint num = 0;
            byte symbol = this.foundState.Symbol;
            if (!skip)
            {
                stateArray[num++] = this.foundState;
                if (context.Suffix == PpmContext.Zero)
                {
                    goto Label_01CC;
                }
            }
            bool flag = false;
            if (state != PpmState.Zero)
            {
                context = context.Suffix;
                flag = true;
            }
        Label_0087:
            if (flag)
            {
                flag = false;
            }
            else
            {
                context = context.Suffix;
                if (context.NumberStatistics != 0)
                {
                    state = context.Statistics;
                    if (state.Symbol != symbol)
                    {
                        do
                        {
                            state2 = state[1];
                            num3 = state2.Symbol;
                            state = PpmState.op_Increment(state);
                        }
                        while (num3 != symbol);
                    }
                    num3 = (state.Frequency < 0x73) ? ((byte) 1) : ((byte) 0);
                    state.Frequency = (byte) (state.Frequency + num3);
                    context.SummaryFrequency = (ushort) (context.SummaryFrequency + num3);
                }
                else
                {
                    state = context.FirstState;
                    state.Frequency = (byte) (state.Frequency + ((byte) (((context.Suffix.NumberStatistics == 0) ? 1 : 0) & ((state.Frequency < 0x18) ? 1 : 0))));
                }
            }
            if (state.Successor != successor)
            {
                context = state.Successor;
            }
            else
            {
                stateArray[num++] = state;
                if (context.Suffix != PpmContext.Zero)
                {
                    goto Label_0087;
                }
            }
        Label_01CC:
            if (num != 0)
            {
                byte firstStateFrequency;
                byte num4 = 0;
                byte num5 = (symbol >= 0x40) ? ((byte) 0x10) : ((byte) 0);
                symbol = successor.NumberStatistics;
                byte num6 = symbol;
                PpmContext context3 = successor + 1;
                num5 = (byte) (num5 | ((symbol >= 0x40) ? ((byte) 8) : ((byte) 0)));
                if (context.NumberStatistics != 0)
                {
                    state = context.Statistics;
                    if (state.Symbol != symbol)
                    {
                        do
                        {
                            state2 = state[1];
                            num3 = state2.Symbol;
                            state = PpmState.op_Increment(state);
                        }
                        while (num3 != symbol);
                    }
                    uint num8 = (uint) (state.Frequency - 1);
                    uint num9 = ((uint) (context.SummaryFrequency - context.NumberStatistics)) - num8;
                    firstStateFrequency = (byte) (1 + (((2 * num8) <= num9) ? (((5 * num8) > num9) ? 1 : 0) : (((num8 + (2 * num9)) - 3) / num9)));
                }
                else
                {
                    firstStateFrequency = context.FirstStateFrequency;
                }
                do
                {
                    PpmContext context4 = this.Allocator.AllocateContext();
                    if (context4 == PpmContext.Zero)
                    {
                        return PpmContext.Zero;
                    }
                    context4.NumberStatistics = num4;
                    context4.Flags = num5;
                    context4.FirstStateSymbol = num6;
                    context4.FirstStateFrequency = firstStateFrequency;
                    context4.FirstStateSuccessor = context3;
                    context4.Suffix = context;
                    context = context4;
                    stateArray[(int) ((IntPtr) (--num))].Successor = context;
                }
                while (num != 0);
            }
            return context;
        }

        private PpmContext CutOff(int order, PpmContext context)
        {
            PpmState firstState;
            if (context.NumberStatistics == 0)
            {
                firstState = context.FirstState;
                if (firstState.Successor >= this.Allocator.BaseUnit)
                {
                    if (order < this.modelOrder)
                    {
                        firstState.Successor = this.CutOff(order + 1, firstState.Successor);
                    }
                    else
                    {
                        firstState.Successor = PpmContext.Zero;
                    }
                    if ((firstState.Successor == PpmContext.Zero) && (order > 9L))
                    {
                        this.Allocator.SpecialFreeUnits(context);
                        return PpmContext.Zero;
                    }
                    return context;
                }
                this.Allocator.SpecialFreeUnits(context);
                return PpmContext.Zero;
            }
            uint unitCount = (uint) ((context.NumberStatistics + 2) >> 1);
            context.Statistics = this.Allocator.MoveUnitsUp(context.Statistics, unitCount);
            int numberStatistics = context.NumberStatistics;
            for (firstState = context.Statistics + numberStatistics; firstState >= context.Statistics; firstState = PpmState.op_Decrement(firstState))
            {
                if (firstState.Successor < this.Allocator.BaseUnit)
                {
                    firstState.Successor = PpmContext.Zero;
                    Swap(firstState, context.Statistics[numberStatistics--]);
                }
                else if (order < this.modelOrder)
                {
                    firstState.Successor = this.CutOff(order + 1, firstState.Successor);
                }
                else
                {
                    firstState.Successor = PpmContext.Zero;
                }
            }
            if ((numberStatistics != context.NumberStatistics) && (order != 0))
            {
                context.NumberStatistics = (byte) numberStatistics;
                firstState = context.Statistics;
                if (numberStatistics < 0)
                {
                    this.Allocator.FreeUnits(firstState, unitCount);
                    this.Allocator.SpecialFreeUnits(context);
                    return PpmContext.Zero;
                }
                if (numberStatistics == 0)
                {
                    context.Flags = (byte) ((context.Flags & 0x10) + ((firstState.Symbol >= 0x40) ? 8 : 0));
                    Copy(context.FirstState, firstState);
                    this.Allocator.FreeUnits(firstState, unitCount);
                    context.FirstStateFrequency = (byte) ((context.FirstStateFrequency + 11) >> 3);
                }
                else
                {
                    this.Refresh(unitCount, context.SummaryFrequency > (0x10 * numberStatistics), context);
                }
            }
            return context;
        }

        public void Decode(Stream target, Stream source, PpmdProperties properties)
        {
            int num;
            if (target == null)
            {
                throw new ArgumentNullException("target");
            }
            if (source == null)
            {
                throw new ArgumentNullException("source");
            }
            this.DecodeStart(source, properties);
            byte[] buffer = new byte[0x10000];
            while ((num = this.DecodeBlock(source, buffer, 0, buffer.Length)) != 0)
            {
                target.Write(buffer, 0, num);
            }
        }

        private void DecodeBinarySymbol(PpmContext context)
        {
            PpmState firstState = context.FirstState;
            int num = this.probabilities[firstState.Frequency - 1];
            int num2 = ((this.numberStatisticsToBinarySummaryIndex[context.Suffix.NumberStatistics] + this.previousSuccess) + context.Flags) + ((this.runLength >> 0x1a) & 0x20);
            if (this.Coder.RangeGetCurrentShiftCount(14) < this.binarySummary[num, num2])
            {
                this.foundState = firstState;
                firstState.Frequency = (byte) (firstState.Frequency + ((firstState.Frequency < 0xc4) ? ((byte) 1) : ((byte) 0)));
                this.Coder.LowCount = 0;
                this.Coder.HighCount = this.binarySummary[num, num2];
                ushort num1 = this.binarySummary[num, num2];
                num1[0] = (ushort) (num1[0] + ((ushort) (0x80L - Mean(this.binarySummary[num, num2], 7, 2))));
                this.previousSuccess = 1;
                this.runLength++;
            }
            else
            {
                this.Coder.LowCount = this.binarySummary[num, num2];
                ushort num3 = this.binarySummary[num, num2];
                num3[0] = (ushort) (num3[0] - ((ushort) Mean(this.binarySummary[num, num2], 7, 2)));
                this.Coder.HighCount = 0x4000;
                this.initialEscape = ExponentialEscapes[this.binarySummary[num, num2] >> 10];
                this.characterMask[firstState.Symbol] = this.escapeCount;
                this.previousSuccess = 0;
                this.numberMasked = 0;
                this.foundState = PpmState.Zero;
            }
        }

        internal int DecodeBlock(Stream source, byte[] buffer, int offset, int count)
        {
            if (this.minimumContext == PpmContext.Zero)
            {
                return 0;
            }
            int num = 0;
            while (num < count)
            {
                if (this.numberStatistics != 0)
                {
                    this.DecodeSymbol1(this.minimumContext);
                }
                else
                {
                    this.DecodeBinarySymbol(this.minimumContext);
                }
                this.Coder.RangeRemoveSubrange();
                while (this.foundState == PpmState.Zero)
                {
                    this.Coder.RangeDecoderNormalize(source);
                    do
                    {
                        this.orderFall++;
                        this.minimumContext = this.minimumContext.Suffix;
                        if (this.minimumContext == PpmContext.Zero)
                        {
                            return num;
                        }
                    }
                    while (this.minimumContext.NumberStatistics == this.numberMasked);
                    this.DecodeSymbol2(this.minimumContext);
                    this.Coder.RangeRemoveSubrange();
                }
                buffer[offset] = this.foundState.Symbol;
                offset++;
                num++;
                if ((this.orderFall == 0) && (this.foundState.Successor >= this.Allocator.BaseUnit))
                {
                    this.maximumContext = this.foundState.Successor;
                }
                else
                {
                    this.UpdateModel(this.minimumContext);
                    if (this.escapeCount == 0)
                    {
                        this.ClearMask();
                    }
                }
                this.minimumContext = this.maximumContext;
                this.numberStatistics = this.minimumContext.NumberStatistics;
                this.Coder.RangeDecoderNormalize(source);
            }
            return num;
        }

        internal SharpCompress.Compressor.PPMd.I1.Coder DecodeStart(Stream source, PpmdProperties properties)
        {
            this.Allocator = properties.Allocator;
            this.Coder = new SharpCompress.Compressor.PPMd.I1.Coder();
            this.Coder.RangeDecoderInitialize(source);
            this.StartModel(properties.ModelOrder, properties.ModelRestorationMethod);
            this.minimumContext = this.maximumContext;
            this.numberStatistics = this.minimumContext.NumberStatistics;
            return this.Coder;
        }

        private void DecodeSymbol1(PpmContext context)
        {
            PpmState state2;
            uint frequency = context.Statistics.Frequency;
            PpmState statistics = context.Statistics;
            this.Coder.Scale = context.SummaryFrequency;
            uint num2 = this.Coder.RangeGetCurrentCount();
            if (num2 < frequency)
            {
                this.Coder.HighCount = frequency;
                this.previousSuccess = ((2 * this.Coder.HighCount) >= this.Coder.Scale) ? ((byte) 1) : ((byte) 0);
                this.foundState = statistics;
                frequency += 4;
                this.foundState.Frequency = (byte) frequency;
                context.SummaryFrequency = (ushort) (context.SummaryFrequency + 4);
                this.runLength += this.previousSuccess;
                if (frequency > 0x7c)
                {
                    this.Rescale(context);
                }
                this.Coder.LowCount = 0;
                return;
            }
            uint numberStatistics = context.NumberStatistics;
            this.previousSuccess = 0;
        Label_0193:
            state2 = statistics = PpmState.op_Increment(statistics);
            if ((frequency += state2.Frequency) <= num2)
            {
                if (--numberStatistics == 0)
                {
                    this.Coder.LowCount = frequency;
                    this.characterMask[statistics.Symbol] = this.escapeCount;
                    this.numberMasked = context.NumberStatistics;
                    numberStatistics = context.NumberStatistics;
                    this.foundState = PpmState.Zero;
                    do
                    {
                        state2 = statistics = PpmState.op_Decrement(statistics);
                        this.characterMask[state2.Symbol] = this.escapeCount;
                    }
                    while (--numberStatistics != 0);
                    this.Coder.HighCount = this.Coder.Scale;
                    return;
                }
                goto Label_0193;
            }
            this.Coder.HighCount = frequency;
            this.Coder.LowCount = this.Coder.HighCount - statistics.Frequency;
            this.Update1(statistics, context);
        }

        private void DecodeSymbol2(PpmContext context)
        {
            See2Context context2 = this.MakeEscapeFrequency(context);
            uint num3 = 0;
            uint num4 = (uint) (context.NumberStatistics - this.numberMasked);
            uint index = 0;
            PpmState state = context.Statistics - 1;
            do
            {
                uint symbol;
                do
                {
                    PpmState state2 = state[1];
                    symbol = state2.Symbol;
                    state = PpmState.op_Increment(state);
                }
                while (this.characterMask[symbol] == this.escapeCount);
                num3 += state.Frequency;
                this.decodeStates[index++] = state;
            }
            while (--num4 != 0);
            this.Coder.Scale += num3;
            uint num2 = this.Coder.RangeGetCurrentCount();
            index = 0;
            state = this.decodeStates[index];
            if (num2 < num3)
            {
                num3 = 0;
                while ((num3 += state.Frequency) <= num2)
                {
                    state = this.decodeStates[(int) ((IntPtr) (++index))];
                }
                this.Coder.HighCount = num3;
                this.Coder.LowCount = this.Coder.HighCount - state.Frequency;
                context2.Update();
                this.Update2(state, context);
            }
            else
            {
                this.Coder.LowCount = num3;
                this.Coder.HighCount = this.Coder.Scale;
                num4 = (uint) (context.NumberStatistics - this.numberMasked);
                this.numberMasked = context.NumberStatistics;
                do
                {
                    this.characterMask[this.decodeStates[index].Symbol] = this.escapeCount;
                    index++;
                }
                while (--num4 != 0);
                context2.Summary = (ushort) (context2.Summary + ((ushort) this.Coder.Scale));
            }
        }

        public void Encode(Stream target, Stream source, PpmdProperties properties)
        {
            if (target == null)
            {
                throw new ArgumentNullException("target");
            }
            if (source == null)
            {
                throw new ArgumentNullException("source");
            }
            this.EncodeStart(properties);
            this.EncodeBlock(target, source, true);
        }

        private void EncodeBinarySymbol(int symbol, PpmContext context)
        {
            PpmState firstState = context.FirstState;
            int num = this.probabilities[firstState.Frequency - 1];
            int num2 = ((this.numberStatisticsToBinarySummaryIndex[context.Suffix.NumberStatistics] + this.previousSuccess) + context.Flags) + ((this.runLength >> 0x1a) & 0x20);
            if (firstState.Symbol == symbol)
            {
                this.foundState = firstState;
                firstState.Frequency = (byte) (firstState.Frequency + ((firstState.Frequency < 0xc4) ? ((byte) 1) : ((byte) 0)));
                this.Coder.LowCount = 0;
                this.Coder.HighCount = this.binarySummary[num, num2];
                ushort num1 = this.binarySummary[num, num2];
                num1[0] = (ushort) (num1[0] + ((ushort) (0x80L - Mean(this.binarySummary[num, num2], 7, 2))));
                this.previousSuccess = 1;
                this.runLength++;
            }
            else
            {
                this.Coder.LowCount = this.binarySummary[num, num2];
                ushort num3 = this.binarySummary[num, num2];
                num3[0] = (ushort) (num3[0] - ((ushort) Mean(this.binarySummary[num, num2], 7, 2)));
                this.Coder.HighCount = 0x4000;
                this.initialEscape = ExponentialEscapes[this.binarySummary[num, num2] >> 10];
                this.characterMask[firstState.Symbol] = this.escapeCount;
                this.previousSuccess = 0;
                this.numberMasked = 0;
                this.foundState = PpmState.Zero;
            }
        }

        internal void EncodeBlock(Stream target, Stream source, bool final)
        {
            while (true)
            {
                this.minimumContext = this.maximumContext;
                this.numberStatistics = this.minimumContext.NumberStatistics;
                int symbol = source.ReadByte();
                if (!((symbol >= 0) || final))
                {
                    return;
                }
                if (this.numberStatistics != 0)
                {
                    this.EncodeSymbol1(symbol, this.minimumContext);
                    this.Coder.RangeEncodeSymbol();
                }
                else
                {
                    this.EncodeBinarySymbol(symbol, this.minimumContext);
                    this.Coder.RangeShiftEncodeSymbol(14);
                }
                while (this.foundState == PpmState.Zero)
                {
                    this.Coder.RangeEncoderNormalize(target);
                    do
                    {
                        this.orderFall++;
                        this.minimumContext = this.minimumContext.Suffix;
                        if (this.minimumContext == PpmContext.Zero)
                        {
                            this.Coder.RangeEncoderFlush(target);
                            return;
                        }
                    }
                    while (this.minimumContext.NumberStatistics == this.numberMasked);
                    this.EncodeSymbol2(symbol, this.minimumContext);
                    this.Coder.RangeEncodeSymbol();
                }
                if ((this.orderFall == 0) && (this.foundState.Successor >= this.Allocator.BaseUnit))
                {
                    this.maximumContext = this.foundState.Successor;
                }
                else
                {
                    this.UpdateModel(this.minimumContext);
                    if (this.escapeCount == 0)
                    {
                        this.ClearMask();
                    }
                }
                this.Coder.RangeEncoderNormalize(target);
            }
        }

        internal SharpCompress.Compressor.PPMd.I1.Coder EncodeStart(PpmdProperties properties)
        {
            this.Allocator = properties.Allocator;
            this.Coder = new SharpCompress.Compressor.PPMd.I1.Coder();
            this.Coder.RangeEncoderInitialize();
            this.StartModel(properties.ModelOrder, properties.ModelRestorationMethod);
            return this.Coder;
        }

        private void EncodeSymbol1(int symbol, PpmContext context)
        {
            PpmState state2;
            uint numberStatistics = context.Statistics.Symbol;
            PpmState statistics = context.Statistics;
            this.Coder.Scale = context.SummaryFrequency;
            if (numberStatistics == symbol)
            {
                this.Coder.HighCount = statistics.Frequency;
                this.previousSuccess = ((2 * this.Coder.HighCount) >= this.Coder.Scale) ? ((byte) 1) : ((byte) 0);
                this.foundState = statistics;
                this.foundState.Frequency = (byte) (this.foundState.Frequency + 4);
                context.SummaryFrequency = (ushort) (context.SummaryFrequency + 4);
                this.runLength += this.previousSuccess;
                if (statistics.Frequency > 0x7c)
                {
                    this.Rescale(context);
                }
                this.Coder.LowCount = 0;
                return;
            }
            uint frequency = statistics.Frequency;
            numberStatistics = context.NumberStatistics;
            this.previousSuccess = 0;
        Label_01A8:
            state2 = statistics = PpmState.op_Increment(statistics);
            if (state2.Symbol != symbol)
            {
                frequency += statistics.Frequency;
                if (--numberStatistics == 0)
                {
                    this.Coder.LowCount = frequency;
                    this.characterMask[statistics.Symbol] = this.escapeCount;
                    this.numberMasked = context.NumberStatistics;
                    numberStatistics = context.NumberStatistics;
                    this.foundState = PpmState.Zero;
                    do
                    {
                        state2 = statistics = PpmState.op_Decrement(statistics);
                        this.characterMask[state2.Symbol] = this.escapeCount;
                    }
                    while (--numberStatistics != 0);
                    this.Coder.HighCount = this.Coder.Scale;
                    return;
                }
                goto Label_01A8;
            }
            this.Coder.HighCount = (this.Coder.LowCount = frequency) + statistics.Frequency;
            this.Update1(statistics, context);
        }

        private void EncodeSymbol2(int symbol, PpmContext context)
        {
            See2Context context2 = this.MakeEscapeFrequency(context);
            uint num2 = 0;
            uint num3 = (uint) (context.NumberStatistics - this.numberMasked);
            PpmState state = context.Statistics - 1;
            do
            {
                uint num;
                PpmState state3;
                do
                {
                    state3 = state[1];
                    num = state3.Symbol;
                    state = PpmState.op_Increment(state);
                }
                while (this.characterMask[num] == this.escapeCount);
                this.characterMask[num] = this.escapeCount;
                if (num == symbol)
                {
                    this.Coder.LowCount = num2;
                    num2 += state.Frequency;
                    this.Coder.HighCount = num2;
                    PpmState state2 = state;
                    while (--num3 != 0)
                    {
                        do
                        {
                            state3 = state2[1];
                            num = state3.Symbol;
                            state2 = PpmState.op_Increment(state2);
                        }
                        while (this.characterMask[num] == this.escapeCount);
                        num2 += state2.Frequency;
                    }
                    this.Coder.Scale += num2;
                    context2.Update();
                    this.Update2(state, context);
                    return;
                }
                num2 += state.Frequency;
            }
            while (--num3 != 0);
            this.Coder.LowCount = num2;
            this.Coder.Scale += this.Coder.LowCount;
            this.Coder.HighCount = this.Coder.Scale;
            context2.Summary = (ushort) (context2.Summary + ((ushort) this.Coder.Scale));
            this.numberMasked = context.NumberStatistics;
        }

        private See2Context MakeEscapeFrequency(PpmContext context)
        {
            See2Context context2;
            uint numberStatistics = (uint) (2 * context.NumberStatistics);
            if (context.NumberStatistics != 0xff)
            {
                numberStatistics = context.Suffix.NumberStatistics;
                int num2 = this.probabilities[context.NumberStatistics + 2] - 3;
                int num3 = (((context.SummaryFrequency > (11 * (context.NumberStatistics + 1))) ? 1 : 0) + (((2 * context.NumberStatistics) < (numberStatistics + this.numberMasked)) ? 2 : 0)) + context.Flags;
                context2 = this.see2Contexts[num2, num3];
                this.Coder.Scale = context2.Mean();
                return context2;
            }
            context2 = this.emptySee2Context;
            this.Coder.Scale = 1;
            return context2;
        }

        private static int Mean(int sum, int shift, int round)
        {
            return ((sum + (1 << ((shift - round) & 0x1f))) >> shift);
        }

        private PpmContext ReduceOrder(PpmState state, PpmContext context)
        {
            PpmState[] stateArray = new PpmState[0x10];
            uint num = 0;
            PpmContext context2 = context;
            PpmContext text = this.Allocator.Text;
            byte symbol = this.foundState.Symbol;
            stateArray[num++] = this.foundState;
            this.foundState.Successor = text;
            this.orderFall++;
            bool flag = false;
            if (state != PpmState.Zero)
            {
                context = context.Suffix;
                flag = true;
            }
            while (true)
            {
                if (flag)
                {
                    flag = false;
                }
                else
                {
                    if (context.Suffix == PpmContext.Zero)
                    {
                        if (this.method > ModelRestorationMethod.Freeze)
                        {
                            do
                            {
                                stateArray[(int) ((IntPtr) (--num))].Successor = context;
                            }
                            while (num != 0);
                            this.Allocator.Text = this.Allocator.Heap + 1;
                            this.orderFall = 1;
                        }
                        return context;
                    }
                    context = context.Suffix;
                    if (context.NumberStatistics != 0)
                    {
                        byte num2;
                        state = context.Statistics;
                        if (state.Symbol != symbol)
                        {
                            do
                            {
                                PpmState state3 = state[1];
                                num2 = state3.Symbol;
                                state = PpmState.op_Increment(state);
                            }
                            while (num2 != symbol);
                        }
                        num2 = (state.Frequency < 0x73) ? ((byte) 2) : ((byte) 0);
                        state.Frequency = (byte) (state.Frequency + num2);
                        context.SummaryFrequency = (ushort) (context.SummaryFrequency + num2);
                    }
                    else
                    {
                        state = context.FirstState;
                        state.Frequency = (byte) (state.Frequency + ((state.Frequency < 0x20) ? ((byte) 1) : ((byte) 0)));
                    }
                }
                if (state.Successor != PpmContext.Zero)
                {
                    if (this.method > ModelRestorationMethod.Freeze)
                    {
                        context = state.Successor;
                        do
                        {
                            stateArray[(int) ((IntPtr) (--num))].Successor = context;
                        }
                        while (num != 0);
                        this.Allocator.Text = this.Allocator.Heap + 1;
                        this.orderFall = 1;
                        return context;
                    }
                    if (state.Successor <= text)
                    {
                        PpmState foundState = this.foundState;
                        this.foundState = state;
                        state.Successor = this.CreateSuccessors(false, PpmState.Zero, context);
                        this.foundState = foundState;
                    }
                    if ((this.orderFall == 1) && (context2 == this.maximumContext))
                    {
                        this.foundState.Successor = state.Successor;
                        this.Allocator.Text = Pointer.op_Decrement(this.Allocator.Text);
                    }
                    return state.Successor;
                }
                stateArray[num++] = state;
                state.Successor = text;
                this.orderFall++;
            }
        }

        private void Refresh(uint oldUnitCount, bool scale, PpmContext context)
        {
            int numberStatistics = context.NumberStatistics;
            int num3 = scale ? 1 : 0;
            context.Statistics = this.Allocator.ShrinkUnits(context.Statistics, oldUnitCount, (uint) ((numberStatistics + 2) >> 1));
            PpmState statistics = context.Statistics;
            context.Flags = (byte) ((context.Flags & (0x10 + (scale ? 4 : 0))) + ((statistics.Symbol >= 0x40) ? 8 : 0));
            int num2 = context.SummaryFrequency - statistics.Frequency;
            statistics.Frequency = (byte) ((statistics.Frequency + num3) >> num3);
            context.SummaryFrequency = statistics.Frequency;
            do
            {
                PpmState state2 = statistics = PpmState.op_Increment(statistics);
                num2 -= state2.Frequency;
                statistics.Frequency = (byte) ((statistics.Frequency + num3) >> num3);
                context.SummaryFrequency = (ushort) (context.SummaryFrequency + statistics.Frequency);
                context.Flags = (byte) (context.Flags | ((statistics.Symbol >= 0x40) ? ((byte) 8) : ((byte) 0)));
            }
            while (--numberStatistics != 0);
            num2 = (num2 + num3) >> num3;
            context.SummaryFrequency = (ushort) (context.SummaryFrequency + ((ushort) num2));
        }

        private PpmContext RemoveBinaryContexts(int order, PpmContext context)
        {
            PpmState firstState;
            if (context.NumberStatistics == 0)
            {
                firstState = context.FirstState;
                if ((firstState.Successor >= this.Allocator.BaseUnit) && (order < this.modelOrder))
                {
                    firstState.Successor = this.RemoveBinaryContexts(order + 1, firstState.Successor);
                }
                else
                {
                    firstState.Successor = PpmContext.Zero;
                }
                if ((firstState.Successor == PpmContext.Zero) && ((context.Suffix.NumberStatistics == 0) || (context.Suffix.Flags == 0xff)))
                {
                    this.Allocator.FreeUnits(context, 1);
                    return PpmContext.Zero;
                }
                return context;
            }
            for (firstState = context.Statistics + context.NumberStatistics; firstState >= context.Statistics; firstState = PpmState.op_Decrement(firstState))
            {
                if ((firstState.Successor >= this.Allocator.BaseUnit) && (order < this.modelOrder))
                {
                    firstState.Successor = this.RemoveBinaryContexts(order + 1, firstState.Successor);
                }
                else
                {
                    firstState.Successor = PpmContext.Zero;
                }
            }
            return context;
        }

        private void Rescale(PpmContext context)
        {
            byte symbol;
            byte frequency;
            PpmContext successor;
            PpmState state3;
            uint numberStatistics = context.NumberStatistics;
            PpmState foundState = this.foundState;
            while (foundState != context.Statistics)
            {
                Swap(foundState[0], foundState[-1]);
                foundState = PpmState.op_Decrement(foundState);
            }
            foundState.Frequency = (byte) (foundState.Frequency + 4);
            context.SummaryFrequency = (ushort) (context.SummaryFrequency + 4);
            uint num3 = (uint) (context.SummaryFrequency - foundState.Frequency);
            int num2 = ((this.orderFall != 0) || (this.method > ModelRestorationMethod.Freeze)) ? 1 : 0;
            foundState.Frequency = (byte) ((foundState.Frequency + num2) >> 1);
            context.SummaryFrequency = foundState.Frequency;
            do
            {
                state3 = foundState = PpmState.op_Increment(foundState);
                num3 -= state3.Frequency;
                foundState.Frequency = (byte) ((foundState.Frequency + num2) >> 1);
                context.SummaryFrequency = (ushort) (context.SummaryFrequency + foundState.Frequency);
                state3 = foundState[0];
                state3 = foundState[-1];
                if (state3.Frequency > state3.Frequency)
                {
                    PpmState state = foundState;
                    symbol = state.Symbol;
                    frequency = state.Frequency;
                    successor = state.Successor;
                    do
                    {
                        Copy(state[0], state[-1]);
                        state3 = state = PpmState.op_Decrement(state);
                        state3 = state3[-1];
                    }
                    while (frequency > state3.Frequency);
                    state.Symbol = symbol;
                    state.Frequency = frequency;
                    state.Successor = successor;
                }
            }
            while (--numberStatistics != 0);
            if (foundState.Frequency == 0)
            {
                do
                {
                    numberStatistics++;
                    state3 = foundState = PpmState.op_Decrement(foundState);
                }
                while (state3.Frequency == 0);
                num3 += numberStatistics;
                uint unitCount = (uint) ((context.NumberStatistics + 2) >> 1);
                context.NumberStatistics = (byte) (context.NumberStatistics - ((byte) numberStatistics));
                if (context.NumberStatistics == 0)
                {
                    symbol = context.Statistics.Symbol;
                    frequency = context.Statistics.Frequency;
                    successor = context.Statistics.Successor;
                    frequency = (byte) ((((2 * frequency) + num3) - ((ulong) 1L)) / ((ulong) num3));
                    if (frequency > 0x29)
                    {
                        frequency = 0x29;
                    }
                    this.Allocator.FreeUnits(context.Statistics, unitCount);
                    context.FirstStateSymbol = symbol;
                    context.FirstStateFrequency = frequency;
                    context.FirstStateSuccessor = successor;
                    context.Flags = (byte) ((context.Flags & 0x10) + ((symbol >= 0x40) ? 8 : 0));
                    this.foundState = context.FirstState;
                    return;
                }
                context.Statistics = this.Allocator.ShrinkUnits(context.Statistics, unitCount, (uint) ((context.NumberStatistics + 2) >> 1));
                context.Flags = (byte) (context.Flags & 0xf7);
                numberStatistics = context.NumberStatistics;
                foundState = context.Statistics;
                context.Flags = (byte) (context.Flags | ((foundState.Symbol >= 0x40) ? ((byte) 8) : ((byte) 0)));
                do
                {
                    state3 = foundState = PpmState.op_Increment(foundState);
                    context.Flags = (byte) (context.Flags | ((state3.Symbol >= 0x40) ? ((byte) 8) : ((byte) 0)));
                }
                while (--numberStatistics != 0);
            }
            num3 -= num3 >> 1;
            context.SummaryFrequency = (ushort) (context.SummaryFrequency + ((ushort) num3));
            context.Flags = (byte) (context.Flags | 4);
            this.foundState = context.Statistics;
        }

        private void RestoreModel(PpmContext context, PpmContext minimumContext, PpmContext foundStateSuccessor)
        {
            this.Allocator.Text = this.Allocator.Heap;
            PpmContext maximumContext = this.maximumContext;
            while (maximumContext != context)
            {
                if ((maximumContext.NumberStatistics = (byte) (maximumContext.NumberStatistics - 1)) == 0)
                {
                    maximumContext.Flags = (byte) ((maximumContext.Flags & 0x10) + ((maximumContext.Statistics.Symbol >= 0x40) ? 8 : 0));
                    PpmState statistics = maximumContext.Statistics;
                    Copy(maximumContext.FirstState, statistics);
                    this.Allocator.SpecialFreeUnits(statistics);
                    maximumContext.FirstStateFrequency = (byte) ((maximumContext.FirstStateFrequency + 11) >> 3);
                }
                else
                {
                    this.Refresh((uint) ((maximumContext.NumberStatistics + 3) >> 1), false, maximumContext);
                }
                maximumContext = maximumContext.Suffix;
            }
            while (maximumContext != minimumContext)
            {
                if (maximumContext.NumberStatistics == 0)
                {
                    maximumContext.FirstStateFrequency = (byte) (maximumContext.FirstStateFrequency - ((byte) (maximumContext.FirstStateFrequency >> 1)));
                }
                else if ((maximumContext.SummaryFrequency = (ushort) (maximumContext.SummaryFrequency + 4)) > (0x80 + (4 * maximumContext.NumberStatistics)))
                {
                    this.Refresh((uint) ((maximumContext.NumberStatistics + 2) >> 1), true, maximumContext);
                }
                maximumContext = maximumContext.Suffix;
            }
            if (this.method > ModelRestorationMethod.Freeze)
            {
                this.maximumContext = foundStateSuccessor;
                this.Allocator.GlueCount += ((this.Allocator.MemoryNodes[1].Stamp & 1) == 0) ? 1 : 0;
            }
            else if (this.method == ModelRestorationMethod.Freeze)
            {
                while (this.maximumContext.Suffix != PpmContext.Zero)
                {
                    this.maximumContext = this.maximumContext.Suffix;
                }
                this.RemoveBinaryContexts(0, this.maximumContext);
                this.method += 1;
                this.Allocator.GlueCount = 0;
                this.orderFall = this.modelOrder;
            }
            else if ((this.method == ModelRestorationMethod.Restart) || (this.Allocator.GetMemoryUsed() < (this.Allocator.AllocatorSize >> 1)))
            {
                this.StartModel(this.modelOrder, this.method);
                this.escapeCount = 0;
            }
            else
            {
                while (this.maximumContext.Suffix != PpmContext.Zero)
                {
                    this.maximumContext = this.maximumContext.Suffix;
                }
                do
                {
                    this.CutOff(0, this.maximumContext);
                    this.Allocator.ExpandText();
                }
                while (this.Allocator.GetMemoryUsed() > (3 * (this.Allocator.AllocatorSize >> 2)));
                this.Allocator.GlueCount = 0;
                this.orderFall = this.modelOrder;
            }
        }

        private void StartModel(int modelOrder, ModelRestorationMethod modelRestorationMethod)
        {
            Array.Clear(this.characterMask, 0, this.characterMask.Length);
            this.escapeCount = 1;
            if (modelOrder < 2)
            {
                this.orderFall = this.modelOrder;
                for (PpmContext context = this.maximumContext; context.Suffix != PpmContext.Zero; context = context.Suffix)
                {
                    this.orderFall--;
                }
            }
            else
            {
                int num4;
                this.modelOrder = modelOrder;
                this.orderFall = modelOrder;
                this.method = modelRestorationMethod;
                this.Allocator.Initialize();
                this.initialRunLength = -((modelOrder < 12) ? modelOrder : 12) - 1;
                this.runLength = this.initialRunLength;
                this.maximumContext = this.Allocator.AllocateContext();
                this.maximumContext.Suffix = PpmContext.Zero;
                this.maximumContext.NumberStatistics = 0xff;
                this.maximumContext.SummaryFrequency = (ushort) (this.maximumContext.NumberStatistics + 2);
                this.maximumContext.Statistics = this.Allocator.AllocateUnits(0x80);
                this.previousSuccess = 0;
                for (int i = 0; i < 0x100; i++)
                {
                    PpmState state = this.maximumContext.Statistics[i];
                    state.Symbol = (byte) i;
                    state.Frequency = 1;
                    state.Successor = PpmContext.Zero;
                }
                uint num2 = 0;
                int index = 0;
                while (num2 < 0x19)
                {
                    while (this.probabilities[index] == num2)
                    {
                        index++;
                    }
                    num4 = 0;
                    while (num4 < 8)
                    {
                        this.binarySummary[(int) ((IntPtr) num2), (int) ((IntPtr) num4)] = (ushort) (0x4000L - (InitialBinaryEscapes[num4] / (index + 1)));
                        num4++;
                    }
                    num4 = 8;
                    while (num4 < 0x40)
                    {
                        for (int j = 0; j < 8; j++)
                        {
                            this.binarySummary[(int) ((IntPtr) num2), (int) ((IntPtr) (num4 + j))] = this.binarySummary[(int) ((IntPtr) num2), (int) ((IntPtr) j)];
                        }
                        num4 += 8;
                    }
                    num2++;
                }
                num2 = 0;
                uint num6 = 0;
                while (num2 < 0x18)
                {
                    while (this.probabilities[(int) ((IntPtr) (num6 + 3))] == (num2 + 3))
                    {
                        num6++;
                    }
                    for (num4 = 0; num4 < 0x20; num4++)
                    {
                        this.see2Contexts[(int) ((IntPtr) num2), (int) ((IntPtr) num4)].Initialize((2 * num6) + 5);
                    }
                    num2++;
                }
            }
        }

        private static void Swap(PpmState state1, PpmState state2)
        {
            byte symbol = state1.Symbol;
            byte frequency = state1.Frequency;
            PpmContext successor = state1.Successor;
            state1.Symbol = state2.Symbol;
            state1.Frequency = state2.Frequency;
            state1.Successor = state2.Successor;
            state2.Symbol = symbol;
            state2.Frequency = frequency;
            state2.Successor = successor;
        }

        private void Update1(PpmState state, PpmContext context)
        {
            this.foundState = state;
            this.foundState.Frequency = (byte) (this.foundState.Frequency + 4);
            context.SummaryFrequency = (ushort) (context.SummaryFrequency + 4);
            PpmState state2 = state[0];
            state2 = state[-1];
            if (state2.Frequency > state2.Frequency)
            {
                Swap(state[0], state[-1]);
                this.foundState = state = PpmState.op_Decrement(state);
                if (state.Frequency > 0x7c)
                {
                    this.Rescale(context);
                }
            }
        }

        private void Update2(PpmState state, PpmContext context)
        {
            this.foundState = state;
            this.foundState.Frequency = (byte) (this.foundState.Frequency + 4);
            context.SummaryFrequency = (ushort) (context.SummaryFrequency + 4);
            if (state.Frequency > 0x7c)
            {
                this.Rescale(context);
            }
            this.escapeCount = (byte) (this.escapeCount + 1);
            this.runLength = this.initialRunLength;
        }

        private void UpdateModel(PpmContext minimumContext)
        {
            uint num3;
            PpmState zero = PpmState.Zero;
            PpmContext maximumContext = this.maximumContext;
            uint frequency = this.foundState.Frequency;
            byte symbol = this.foundState.Symbol;
            PpmContext successor = this.foundState.Successor;
            PpmContext suffix = minimumContext.Suffix;
            if ((frequency < 0x1f) && (suffix != PpmContext.Zero))
            {
                if (suffix.NumberStatistics != 0)
                {
                    zero = suffix.Statistics;
                    if (zero.Symbol != symbol)
                    {
                        byte num8;
                        PpmState state2;
                        do
                        {
                            state2 = zero[1];
                            num8 = state2.Symbol;
                            zero = PpmState.op_Increment(zero);
                        }
                        while (num8 != symbol);
                        state2 = zero[0];
                        state2 = zero[-1];
                        if (state2.Frequency >= state2.Frequency)
                        {
                            Swap(zero[0], zero[-1]);
                            zero = PpmState.op_Decrement(zero);
                        }
                    }
                    num3 = (zero.Frequency < 0x73) ? 2 : 0;
                    zero.Frequency = (byte) (zero.Frequency + ((byte) num3));
                    suffix.SummaryFrequency = (ushort) (suffix.SummaryFrequency + ((byte) num3));
                }
                else
                {
                    zero = suffix.FirstState;
                    zero.Frequency = (byte) (zero.Frequency + ((zero.Frequency < 0x20) ? ((byte) 1) : ((byte) 0)));
                }
            }
            if ((this.orderFall == 0) && (successor != PpmContext.Zero))
            {
                this.foundState.Successor = this.CreateSuccessors(true, zero, minimumContext);
                if (this.foundState.Successor != PpmContext.Zero)
                {
                    this.maximumContext = this.foundState.Successor;
                    return;
                }
            }
            else
            {
                this.Allocator.Text[0] = symbol;
                this.Allocator.Text = Pointer.op_Increment(this.Allocator.Text);
                PpmContext text = this.Allocator.Text;
                if (this.Allocator.Text < this.Allocator.BaseUnit)
                {
                    if (successor != PpmContext.Zero)
                    {
                        if (successor < this.Allocator.BaseUnit)
                        {
                            successor = this.CreateSuccessors(false, zero, minimumContext);
                        }
                    }
                    else
                    {
                        successor = this.ReduceOrder(zero, minimumContext);
                    }
                    if (successor != PpmContext.Zero)
                    {
                        if (--this.orderFall == 0)
                        {
                            text = successor;
                            this.Allocator.Text -= (this.maximumContext != minimumContext) ? 1 : 0;
                        }
                        else if (this.method > ModelRestorationMethod.Freeze)
                        {
                            text = successor;
                            this.Allocator.Text = this.Allocator.Heap;
                            this.orderFall = 0;
                        }
                        uint numberStatistics = minimumContext.NumberStatistics;
                        uint num5 = (minimumContext.SummaryFrequency - numberStatistics) - frequency;
                        byte num9 = (symbol >= 0x40) ? ((byte) 8) : ((byte) 0);
                        while (maximumContext != minimumContext)
                        {
                            byte num11;
                            uint num2 = maximumContext.NumberStatistics;
                            if (num2 != 0)
                            {
                                if ((num2 & 1) != 0)
                                {
                                    zero = this.Allocator.ExpandUnits(maximumContext.Statistics, (uint) ((num2 + 1) >> 1));
                                    if (zero == PpmState.Zero)
                                    {
                                        goto Label_059D;
                                    }
                                    maximumContext.Statistics = zero;
                                }
                                maximumContext.SummaryFrequency = (ushort) (maximumContext.SummaryFrequency + ((((3 * num2) + 1) < numberStatistics) ? ((ushort) 1) : ((ushort) 0)));
                            }
                            else
                            {
                                zero = this.Allocator.AllocateUnits(1);
                                if (zero == PpmState.Zero)
                                {
                                    goto Label_059D;
                                }
                                Copy(zero, maximumContext.FirstState);
                                maximumContext.Statistics = zero;
                                if (zero.Frequency < 30)
                                {
                                    zero.Frequency = (byte) (zero.Frequency + zero.Frequency);
                                }
                                else
                                {
                                    zero.Frequency = 120;
                                }
                                maximumContext.SummaryFrequency = (ushort) ((zero.Frequency + this.initialEscape) + ((numberStatistics > 2) ? 1 : 0));
                            }
                            num3 = (uint) ((2 * frequency) * (maximumContext.SummaryFrequency + 6));
                            uint num4 = num5 + maximumContext.SummaryFrequency;
                            if (num3 < (6 * num4))
                            {
                                num3 = (uint) ((1 + ((num3 > num4) ? 1 : 0)) + ((num3 >= (4 * num4)) ? 1 : 0));
                                maximumContext.SummaryFrequency = (ushort) (maximumContext.SummaryFrequency + 4);
                            }
                            else
                            {
                                num3 = (uint) (((4 + ((num3 > (9 * num4)) ? 1 : 0)) + ((num3 > (12 * num4)) ? 1 : 0)) + ((num3 > (15 * num4)) ? 1 : 0));
                                maximumContext.SummaryFrequency = (ushort) (maximumContext.SummaryFrequency + ((ushort) num3));
                            }
                            maximumContext.NumberStatistics = num11 = (byte) (maximumContext.NumberStatistics + 1);
                            zero = maximumContext.Statistics + num11;
                            zero.Successor = text;
                            zero.Symbol = symbol;
                            zero.Frequency = (byte) num3;
                            maximumContext.Flags = (byte) (maximumContext.Flags | num9);
                            maximumContext = maximumContext.Suffix;
                        }
                        this.maximumContext = successor;
                        return;
                    }
                }
            }
        Label_059D:
            this.RestoreModel(maximumContext, minimumContext, successor);
        }

        [StructLayout(LayoutKind.Sequential)]
        internal struct PpmContext
        {
            public const int Size = 12;
            public uint Address;
            public byte[] Memory;
            public static readonly Model.PpmContext Zero;
            public PpmContext(uint address, byte[] memory)
            {
                this.Address = address;
                this.Memory = memory;
            }

            public byte NumberStatistics
            {
                get
                {
                    return this.Memory[this.Address];
                }
                set
                {
                    this.Memory[this.Address] = value;
                }
            }
            public byte Flags
            {
                get
                {
                    return this.Memory[(int) ((IntPtr) (this.Address + 1))];
                }
                set
                {
                    this.Memory[(int) ((IntPtr) (this.Address + 1))] = value;
                }
            }
            public ushort SummaryFrequency
            {
                get
                {
                    return (ushort) (this.Memory[(int) ((IntPtr) (this.Address + 2))] | (this.Memory[(int) ((IntPtr) (this.Address + 3))] << 8));
                }
                set
                {
                    this.Memory[(int) ((IntPtr) (this.Address + 2))] = (byte) value;
                    this.Memory[(int) ((IntPtr) (this.Address + 3))] = (byte) (value >> 8);
                }
            }
            public PpmState Statistics
            {
                get
                {
                    return new PpmState((uint) (((this.Memory[(int) ((IntPtr) (this.Address + 4))] | (this.Memory[(int) ((IntPtr) (this.Address + 5))] << 8)) | (this.Memory[(int) ((IntPtr) (this.Address + 6))] << 0x10)) | (this.Memory[(int) ((IntPtr) (this.Address + 7))] << 0x18)), this.Memory);
                }
                set
                {
                    this.Memory[(int) ((IntPtr) (this.Address + 4))] = (byte) value.Address;
                    this.Memory[(int) ((IntPtr) (this.Address + 5))] = (byte) (value.Address >> 8);
                    this.Memory[(int) ((IntPtr) (this.Address + 6))] = (byte) (value.Address >> 0x10);
                    this.Memory[(int) ((IntPtr) (this.Address + 7))] = (byte) (value.Address >> 0x18);
                }
            }
            public Model.PpmContext Suffix
            {
                get
                {
                    return new Model.PpmContext((uint) (((this.Memory[(int) ((IntPtr) (this.Address + 8))] | (this.Memory[(int) ((IntPtr) (this.Address + 9))] << 8)) | (this.Memory[(int) ((IntPtr) (this.Address + 10))] << 0x10)) | (this.Memory[(int) ((IntPtr) (this.Address + 11))] << 0x18)), this.Memory);
                }
                set
                {
                    this.Memory[(int) ((IntPtr) (this.Address + 8))] = (byte) value.Address;
                    this.Memory[(int) ((IntPtr) (this.Address + 9))] = (byte) (value.Address >> 8);
                    this.Memory[(int) ((IntPtr) (this.Address + 10))] = (byte) (value.Address >> 0x10);
                    this.Memory[(int) ((IntPtr) (this.Address + 11))] = (byte) (value.Address >> 0x18);
                }
            }
            public PpmState FirstState
            {
                get
                {
                    return new PpmState(this.Address + 2, this.Memory);
                }
            }
            public byte FirstStateSymbol
            {
                get
                {
                    return this.Memory[(int) ((IntPtr) (this.Address + 2))];
                }
                set
                {
                    this.Memory[(int) ((IntPtr) (this.Address + 2))] = value;
                }
            }
            public byte FirstStateFrequency
            {
                get
                {
                    return this.Memory[(int) ((IntPtr) (this.Address + 3))];
                }
                set
                {
                    this.Memory[(int) ((IntPtr) (this.Address + 3))] = value;
                }
            }
            public Model.PpmContext FirstStateSuccessor
            {
                get
                {
                    return new Model.PpmContext((uint) (((this.Memory[(int) ((IntPtr) (this.Address + 4))] | (this.Memory[(int) ((IntPtr) (this.Address + 5))] << 8)) | (this.Memory[(int) ((IntPtr) (this.Address + 6))] << 0x10)) | (this.Memory[(int) ((IntPtr) (this.Address + 7))] << 0x18)), this.Memory);
                }
                set
                {
                    this.Memory[(int) ((IntPtr) (this.Address + 4))] = (byte) value.Address;
                    this.Memory[(int) ((IntPtr) (this.Address + 5))] = (byte) (value.Address >> 8);
                    this.Memory[(int) ((IntPtr) (this.Address + 6))] = (byte) (value.Address >> 0x10);
                    this.Memory[(int) ((IntPtr) (this.Address + 7))] = (byte) (value.Address >> 0x18);
                }
            }
            public static implicit operator Model.PpmContext(Pointer pointer)
            {
                return new Model.PpmContext(pointer.Address, pointer.Memory);
            }

            public static Model.PpmContext operator +(Model.PpmContext context, int offset)
            {
                context.Address += (uint) (offset * 12);
                return context;
            }

            public static Model.PpmContext operator -(Model.PpmContext context, int offset)
            {
                context.Address -= (uint) (offset * 12);
                return context;
            }

            public static bool operator <=(Model.PpmContext context1, Model.PpmContext context2)
            {
                return (context1.Address <= context2.Address);
            }

            public static bool operator >=(Model.PpmContext context1, Model.PpmContext context2)
            {
                return (context1.Address >= context2.Address);
            }

            public static bool operator ==(Model.PpmContext context1, Model.PpmContext context2)
            {
                return (context1.Address == context2.Address);
            }

            public static bool operator !=(Model.PpmContext context1, Model.PpmContext context2)
            {
                return (context1.Address != context2.Address);
            }

            public override bool Equals(object obj)
            {
                if (obj is Model.PpmContext)
                {
                    Model.PpmContext context = (Model.PpmContext) obj;
                    return (context.Address == this.Address);
                }
                return base.Equals(obj);
            }

            public override int GetHashCode()
            {
                return this.Address.GetHashCode();
            }

            static PpmContext()
            {
                Zero = new Model.PpmContext(0, null);
            }
        }
    }
}

