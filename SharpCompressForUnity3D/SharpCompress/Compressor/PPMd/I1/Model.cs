namespace SharpCompress.Compressor.PPMd.I1
{
    using SharpCompress.Compressor.PPMd;
    using System;
    using System.IO;
    using System.Runtime.InteropServices;

    //internal class Model
    //{
    //    private SharpCompress.Compressor.PPMd.I1.Allocator Allocator;
    //    private const uint BinaryScale = 0x4000;
    //    private ushort[,] binarySummary;
    //    private byte[] characterMask;
    //    private SharpCompress.Compressor.PPMd.I1.Coder Coder;
    //    private PpmState[] decodeStates;
    //    private See2Context emptySee2Context;
    //    private byte escapeCount;
    //    private static readonly byte[] ExponentialEscapes = new byte[] { 0x19, 14, 9, 7, 5, 5, 4, 4, 4, 3, 3, 3, 2, 2, 2, 2 };
    //    private PpmState foundState;
    //    private static readonly ushort[] InitialBinaryEscapes = new ushort[] { 0x3cdd, 0x1f3f, 0x59bf, 0x48f3, 0x64a1, 0x5abc, 0x6632, 0x6051 };
    //    private int initialEscape;
    //    private int initialRunLength;
    //    private const uint Interval = 0x80;
    //    private const byte IntervalBitCount = 7;
    //    private PpmContext maximumContext;
    //    private const uint MaximumFrequency = 0x7c;
    //    public const int MaximumOrder = 0x10;
    //    private ModelRestorationMethod method;
    //    private PpmContext minimumContext;
    //    private int modelOrder;
    //    private byte numberMasked;
    //    private byte numberStatistics;
    //    private byte[] numberStatisticsToBinarySummaryIndex;
    //    private const uint OrderBound = 9;
    //    private int orderFall;
    //    private const byte PeriodBitCount = 7;
    //    private byte previousSuccess;
    //    private byte[] probabilities;
    //    private int runLength;
    //    private See2Context[,] see2Contexts;
    //    public const uint Signature = 0x84acaf8f;
    //    private const byte TotalBitCount = 14;
    //    private const byte UpperFrequency = 5;
    //    public const char Variant = 'I';

    //    public Model()
    //    {
    //        int num;
    //        this.binarySummary = new ushort[0x19, 0x40];
    //        this.numberStatisticsToBinarySummaryIndex = new byte[0x100];
    //        this.probabilities = new byte[260];
    //        this.characterMask = new byte[0x100];
    //        this.decodeStates = new PpmState[0x100];
    //        this.numberStatisticsToBinarySummaryIndex[0] = 0;
    //        this.numberStatisticsToBinarySummaryIndex[1] = 2;
    //        for (num = 2; num < 11; num++)
    //        {
    //            this.numberStatisticsToBinarySummaryIndex[num] = 4;
    //        }
    //        for (num = 11; num < 0x100; num++)
    //        {
    //            this.numberStatisticsToBinarySummaryIndex[num] = 6;
    //        }
    //        uint num2 = 1;
    //        uint num3 = 1;
    //        uint num4 = 5;
    //        for (num = 0; num < 5; num++)
    //        {
    //            this.probabilities[num] = (byte) num;
    //        }
    //        for (num = 5; num < 260; num++)
    //        {
    //            this.probabilities[num] = (byte) num4;
    //            num2--;
    //            if (num2 == 0)
    //            {
    //                num3++;
    //                num2 = num3;
    //                num4++;
    //            }
    //        }
    //        this.see2Contexts = new See2Context[0x18, 0x20];
    //        for (int i = 0; i < 0x18; i++)
    //        {
    //            for (int j = 0; j < 0x20; j++)
    //            {
    //                this.see2Contexts[i, j] = new See2Context();
    //            }
    //        }
    //        this.emptySee2Context = new See2Context();
    //        this.emptySee2Context.Summary = 0xaf8f;
    //        this.emptySee2Context.Shift = 0xac;
    //        this.emptySee2Context.Count = 0x84;
    //    }

    //    private void ClearMask()
    //    {
    //        this.escapeCount = 1;
    //        Array.Clear(this.characterMask, 0, this.characterMask.Length);
    //    }

    //    private static void Copy(PpmState state1, PpmState state2)
    //    {
    //        state1.Symbol = state2.Symbol;
    //        state1.Frequency = state2.Frequency;
    //        state1.Successor = state2.Successor;
    //    }

    //    private PpmContext CreateSuccessors(bool skip, PpmState state, PpmContext context)
    //    {
    //        byte num3;
    //        PpmState state2;
    //        PpmContext successor = this.foundState.Successor;
    //        PpmState[] stateArray = new PpmState[0x10];
    //        uint num = 0;
    //        byte symbol = this.foundState.Symbol;
    //        if (!skip)
    //        {
    //            stateArray[num++] = this.foundState;
    //            if (context.Suffix == PpmContext.Zero)
    //            {
    //                goto Label_01CC;
    //            }
    //        }
    //        bool flag = false;
    //        if (state != PpmState.Zero)
    //        {
    //            context = context.Suffix;
    //            flag = true;
    //        }
    //    Label_0087:
    //        if (flag)
    //        {
    //            flag = false;
    //        }
    //        else
    //        {
    //            context = context.Suffix;
    //            if (context.NumberStatistics != 0)
    //            {
    //                state = context.Statistics;
    //                if (state.Symbol != symbol)
    //                {
    //                    do
    //                    {
    //                        state2 = state[1];
    //                        num3 = state2.Symbol;
    //                        state = PpmState.op_Increment(state);
    //                    }
    //                    while (num3 != symbol);
    //                }
    //                num3 = (state.Frequency < 0x73) ? ((byte) 1) : ((byte) 0);
    //                state.Frequency = (byte) (state.Frequency + num3);
    //                context.SummaryFrequency = (ushort) (context.SummaryFrequency + num3);
    //            }
    //            else
    //            {
    //                state = context.FirstState;
    //                state.Frequency = (byte) (state.Frequency + ((byte) (((context.Suffix.NumberStatistics == 0) ? 1 : 0) & ((state.Frequency < 0x18) ? 1 : 0))));
    //            }
    //        }
    //        if (state.Successor != successor)
    //        {
    //            context = state.Successor;
    //        }
    //        else
    //        {
    //            stateArray[num++] = state;
    //            if (context.Suffix != PpmContext.Zero)
    //            {
    //                goto Label_0087;
    //            }
    //        }
    //    Label_01CC:
    //        if (num != 0)
    //        {
    //            byte firstStateFrequency;
    //            byte num4 = 0;
    //            byte num5 = (symbol >= 0x40) ? ((byte) 0x10) : ((byte) 0);
    //            symbol = successor.NumberStatistics;
    //            byte num6 = symbol;
    //            PpmContext context3 = successor + 1;
    //            num5 = (byte) (num5 | ((symbol >= 0x40) ? ((byte) 8) : ((byte) 0)));
    //            if (context.NumberStatistics != 0)
    //            {
    //                state = context.Statistics;
    //                if (state.Symbol != symbol)
    //                {
    //                    do
    //                    {
    //                        state2 = state[1];
    //                        num3 = state2.Symbol;
    //                        state = PpmState.op_Increment(state);
    //                    }
    //                    while (num3 != symbol);
    //                }
    //                uint num8 = (uint) (state.Frequency - 1);
    //                uint num9 = ((uint) (context.SummaryFrequency - context.NumberStatistics)) - num8;
    //                firstStateFrequency = (byte) (1 + (((2 * num8) <= num9) ? (((5 * num8) > num9) ? 1 : 0) : (((num8 + (2 * num9)) - 3) / num9)));
    //            }
    //            else
    //            {
    //                firstStateFrequency = context.FirstStateFrequency;
    //            }
    //            do
    //            {
    //                PpmContext context4 = this.Allocator.AllocateContext();
    //                if (context4 == PpmContext.Zero)
    //                {
    //                    return PpmContext.Zero;
    //                }
    //                context4.NumberStatistics = num4;
    //                context4.Flags = num5;
    //                context4.FirstStateSymbol = num6;
    //                context4.FirstStateFrequency = firstStateFrequency;
    //                context4.FirstStateSuccessor = context3;
    //                context4.Suffix = context;
    //                context = context4;
    //                stateArray[(int) ((IntPtr) (--num))].Successor = context;
    //            }
    //            while (num != 0);
    //        }
    //        return context;
    //    }

    //    private PpmContext CutOff(int order, PpmContext context)
    //    {
    //        PpmState firstState;
    //        if (context.NumberStatistics == 0)
    //        {
    //            firstState = context.FirstState;
    //            if (firstState.Successor >= this.Allocator.BaseUnit)
    //            {
    //                if (order < this.modelOrder)
    //                {
    //                    firstState.Successor = this.CutOff(order + 1, firstState.Successor);
    //                }
    //                else
    //                {
    //                    firstState.Successor = PpmContext.Zero;
    //                }
    //                if ((firstState.Successor == PpmContext.Zero) && (order > 9L))
    //                {
    //                    this.Allocator.SpecialFreeUnits(context);
    //                    return PpmContext.Zero;
    //                }
    //                return context;
    //            }
    //            this.Allocator.SpecialFreeUnits(context);
    //            return PpmContext.Zero;
    //        }
    //        uint unitCount = (uint) ((context.NumberStatistics + 2) >> 1);
    //        context.Statistics = this.Allocator.MoveUnitsUp(context.Statistics, unitCount);
    //        int numberStatistics = context.NumberStatistics;
    //        for (firstState = context.Statistics + numberStatistics; firstState >= context.Statistics; firstState = PpmState.op_Decrement(firstState))
    //        {
    //            if (firstState.Successor < this.Allocator.BaseUnit)
    //            {
    //                firstState.Successor = PpmContext.Zero;
    //                Swap(firstState, context.Statistics[numberStatistics--]);
    //            }
    //            else if (order < this.modelOrder)
    //            {
    //                firstState.Successor = this.CutOff(order + 1, firstState.Successor);
    //            }
    //            else
    //            {
    //                firstState.Successor = PpmContext.Zero;
    //            }
    //        }
    //        if ((numberStatistics != context.NumberStatistics) && (order != 0))
    //        {
    //            context.NumberStatistics = (byte) numberStatistics;
    //            firstState = context.Statistics;
    //            if (numberStatistics < 0)
    //            {
    //                this.Allocator.FreeUnits(firstState, unitCount);
    //                this.Allocator.SpecialFreeUnits(context);
    //                return PpmContext.Zero;
    //            }
    //            if (numberStatistics == 0)
    //            {
    //                context.Flags = (byte) ((context.Flags & 0x10) + ((firstState.Symbol >= 0x40) ? 8 : 0));
    //                Copy(context.FirstState, firstState);
    //                this.Allocator.FreeUnits(firstState, unitCount);
    //                context.FirstStateFrequency = (byte) ((context.FirstStateFrequency + 11) >> 3);
    //            }
    //            else
    //            {
    //                this.Refresh(unitCount, context.SummaryFrequency > (0x10 * numberStatistics), context);
    //            }
    //        }
    //        return context;
    //    }

    //    public void Decode(Stream target, Stream source, PpmdProperties properties)
    //    {
    //        int num;
    //        if (target == null)
    //        {
    //            throw new ArgumentNullException("target");
    //        }
    //        if (source == null)
    //        {
    //            throw new ArgumentNullException("source");
    //        }
    //        this.DecodeStart(source, properties);
    //        byte[] buffer = new byte[0x10000];
    //        while ((num = this.DecodeBlock(source, buffer, 0, buffer.Length)) != 0)
    //        {
    //            target.Write(buffer, 0, num);
    //        }
    //    }

    //    private void DecodeBinarySymbol(PpmContext context)
    //    {
    //        PpmState firstState = context.FirstState;
    //        int num = this.probabilities[firstState.Frequency - 1];
    //        int num2 = ((this.numberStatisticsToBinarySummaryIndex[context.Suffix.NumberStatistics] + this.previousSuccess) + context.Flags) + ((this.runLength >> 0x1a) & 0x20);
    //        if (this.Coder.RangeGetCurrentShiftCount(14) < this.binarySummary[num, num2])
    //        {
    //            this.foundState = firstState;
    //            firstState.Frequency = (byte) (firstState.Frequency + ((firstState.Frequency < 0xc4) ? ((byte) 1) : ((byte) 0)));
    //            this.Coder.LowCount = 0;
    //            this.Coder.HighCount = this.binarySummary[num, num2];
    //            ushort num1 = this.binarySummary[num, num2];
    //            num1[0] = (ushort) (num1[0] + ((ushort) (0x80L - Mean(this.binarySummary[num, num2], 7, 2))));
    //            this.previousSuccess = 1;
    //            this.runLength++;
    //        }
    //        else
    //        {
    //            this.Coder.LowCount = this.binarySummary[num, num2];
    //            ushort num3 = this.binarySummary[num, num2];
    //            num3[0] = (ushort) (num3[0] - ((ushort) Mean(this.binarySummary[num, num2], 7, 2)));
    //            this.Coder.HighCount = 0x4000;
    //            this.initialEscape = ExponentialEscapes[this.binarySummary[num, num2] >> 10];
    //            this.characterMask[firstState.Symbol] = this.escapeCount;
    //            this.previousSuccess = 0;
    //            this.numberMasked = 0;
    //            this.foundState = PpmState.Zero;
    //        }
    //    }

    //    internal int DecodeBlock(Stream source, byte[] buffer, int offset, int count)
    //    {
    //        if (this.minimumContext == PpmContext.Zero)
    //        {
    //            return 0;
    //        }
    //        int num = 0;
    //        while (num < count)
    //        {
    //            if (this.numberStatistics != 0)
    //            {
    //                this.DecodeSymbol1(this.minimumContext);
    //            }
    //            else
    //            {
    //                this.DecodeBinarySymbol(this.minimumContext);
    //            }
    //            this.Coder.RangeRemoveSubrange();
    //            while (this.foundState == PpmState.Zero)
    //            {
    //                this.Coder.RangeDecoderNormalize(source);
    //                do
    //                {
    //                    this.orderFall++;
    //                    this.minimumContext = this.minimumContext.Suffix;
    //                    if (this.minimumContext == PpmContext.Zero)
    //                    {
    //                        return num;
    //                    }
    //                }
    //                while (this.minimumContext.NumberStatistics == this.numberMasked);
    //                this.DecodeSymbol2(this.minimumContext);
    //                this.Coder.RangeRemoveSubrange();
    //            }
    //            buffer[offset] = this.foundState.Symbol;
    //            offset++;
    //            num++;
    //            if ((this.orderFall == 0) && (this.foundState.Successor >= this.Allocator.BaseUnit))
    //            {
    //                this.maximumContext = this.foundState.Successor;
    //            }
    //            else
    //            {
    //                this.UpdateModel(this.minimumContext);
    //                if (this.escapeCount == 0)
    //                {
    //                    this.ClearMask();
    //                }
    //            }
    //            this.minimumContext = this.maximumContext;
    //            this.numberStatistics = this.minimumContext.NumberStatistics;
    //            this.Coder.RangeDecoderNormalize(source);
    //        }
    //        return num;
    //    }

    //    internal SharpCompress.Compressor.PPMd.I1.Coder DecodeStart(Stream source, PpmdProperties properties)
    //    {
    //        this.Allocator = properties.Allocator;
    //        this.Coder = new SharpCompress.Compressor.PPMd.I1.Coder();
    //        this.Coder.RangeDecoderInitialize(source);
    //        this.StartModel(properties.ModelOrder, properties.ModelRestorationMethod);
    //        this.minimumContext = this.maximumContext;
    //        this.numberStatistics = this.minimumContext.NumberStatistics;
    //        return this.Coder;
    //    }

    //    private void DecodeSymbol1(PpmContext context)
    //    {
    //        PpmState state2;
    //        uint frequency = context.Statistics.Frequency;
    //        PpmState statistics = context.Statistics;
    //        this.Coder.Scale = context.SummaryFrequency;
    //        uint num2 = this.Coder.RangeGetCurrentCount();
    //        if (num2 < frequency)
    //        {
    //            this.Coder.HighCount = frequency;
    //            this.previousSuccess = ((2 * this.Coder.HighCount) >= this.Coder.Scale) ? ((byte) 1) : ((byte) 0);
    //            this.foundState = statistics;
    //            frequency += 4;
    //            this.foundState.Frequency = (byte) frequency;
    //            context.SummaryFrequency = (ushort) (context.SummaryFrequency + 4);
    //            this.runLength += this.previousSuccess;
    //            if (frequency > 0x7c)
    //            {
    //                this.Rescale(context);
    //            }
    //            this.Coder.LowCount = 0;
    //            return;
    //        }
    //        uint numberStatistics = context.NumberStatistics;
    //        this.previousSuccess = 0;
    //    Label_0193:
    //        state2 = statistics = PpmState.op_Increment(statistics);
    //        if ((frequency += state2.Frequency) <= num2)
    //        {
    //            if (--numberStatistics == 0)
    //            {
    //                this.Coder.LowCount = frequency;
    //                this.characterMask[statistics.Symbol] = this.escapeCount;
    //                this.numberMasked = context.NumberStatistics;
    //                numberStatistics = context.NumberStatistics;
    //                this.foundState = PpmState.Zero;
    //                do
    //                {
    //                    state2 = statistics = PpmState.op_Decrement(statistics);
    //                    this.characterMask[state2.Symbol] = this.escapeCount;
    //                }
    //                while (--numberStatistics != 0);
    //                this.Coder.HighCount = this.Coder.Scale;
    //                return;
    //            }
    //            goto Label_0193;
    //        }
    //        this.Coder.HighCount = frequency;
    //        this.Coder.LowCount = this.Coder.HighCount - statistics.Frequency;
    //        this.Update1(statistics, context);
    //    }

    //    private void DecodeSymbol2(PpmContext context)
    //    {
    //        See2Context context2 = this.MakeEscapeFrequency(context);
    //        uint num3 = 0;
    //        uint num4 = (uint) (context.NumberStatistics - this.numberMasked);
    //        uint index = 0;
    //        PpmState state = context.Statistics - 1;
    //        do
    //        {
    //            uint symbol;
    //            do
    //            {
    //                PpmState state2 = state[1];
    //                symbol = state2.Symbol;
    //                state = PpmState.op_Increment(state);
    //            }
    //            while (this.characterMask[symbol] == this.escapeCount);
    //            num3 += state.Frequency;
    //            this.decodeStates[index++] = state;
    //        }
    //        while (--num4 != 0);
    //        this.Coder.Scale += num3;
    //        uint num2 = this.Coder.RangeGetCurrentCount();
    //        index = 0;
    //        state = this.decodeStates[index];
    //        if (num2 < num3)
    //        {
    //            num3 = 0;
    //            while ((num3 += state.Frequency) <= num2)
    //            {
    //                state = this.decodeStates[(int) ((IntPtr) (++index))];
    //            }
    //            this.Coder.HighCount = num3;
    //            this.Coder.LowCount = this.Coder.HighCount - state.Frequency;
    //            context2.Update();
    //            this.Update2(state, context);
    //        }
    //        else
    //        {
    //            this.Coder.LowCount = num3;
    //            this.Coder.HighCount = this.Coder.Scale;
    //            num4 = (uint) (context.NumberStatistics - this.numberMasked);
    //            this.numberMasked = context.NumberStatistics;
    //            do
    //            {
    //                this.characterMask[this.decodeStates[index].Symbol] = this.escapeCount;
    //                index++;
    //            }
    //            while (--num4 != 0);
    //            context2.Summary = (ushort) (context2.Summary + ((ushort) this.Coder.Scale));
    //        }
    //    }

    //    public void Encode(Stream target, Stream source, PpmdProperties properties)
    //    {
    //        if (target == null)
    //        {
    //            throw new ArgumentNullException("target");
    //        }
    //        if (source == null)
    //        {
    //            throw new ArgumentNullException("source");
    //        }
    //        this.EncodeStart(properties);
    //        this.EncodeBlock(target, source, true);
    //    }

    //    private void EncodeBinarySymbol(int symbol, PpmContext context)
    //    {
    //        PpmState firstState = context.FirstState;
    //        int num = this.probabilities[firstState.Frequency - 1];
    //        int num2 = ((this.numberStatisticsToBinarySummaryIndex[context.Suffix.NumberStatistics] + this.previousSuccess) + context.Flags) + ((this.runLength >> 0x1a) & 0x20);
    //        if (firstState.Symbol == symbol)
    //        {
    //            this.foundState = firstState;
    //            firstState.Frequency = (byte) (firstState.Frequency + ((firstState.Frequency < 0xc4) ? ((byte) 1) : ((byte) 0)));
    //            this.Coder.LowCount = 0;
    //            this.Coder.HighCount = this.binarySummary[num, num2];
    //            ushort num1 = this.binarySummary[num, num2];
    //            num1[0] = (ushort) (num1[0] + ((ushort) (0x80L - Mean(this.binarySummary[num, num2], 7, 2))));
    //            this.previousSuccess = 1;
    //            this.runLength++;
    //        }
    //        else
    //        {
    //            this.Coder.LowCount = this.binarySummary[num, num2];
    //            ushort num3 = this.binarySummary[num, num2];
    //            num3[0] = (ushort) (num3[0] - ((ushort) Mean(this.binarySummary[num, num2], 7, 2)));
    //            this.Coder.HighCount = 0x4000;
    //            this.initialEscape = ExponentialEscapes[this.binarySummary[num, num2] >> 10];
    //            this.characterMask[firstState.Symbol] = this.escapeCount;
    //            this.previousSuccess = 0;
    //            this.numberMasked = 0;
    //            this.foundState = PpmState.Zero;
    //        }
    //    }

    //    internal void EncodeBlock(Stream target, Stream source, bool final)
    //    {
    //        while (true)
    //        {
    //            this.minimumContext = this.maximumContext;
    //            this.numberStatistics = this.minimumContext.NumberStatistics;
    //            int symbol = source.ReadByte();
    //            if (!((symbol >= 0) || final))
    //            {
    //                return;
    //            }
    //            if (this.numberStatistics != 0)
    //            {
    //                this.EncodeSymbol1(symbol, this.minimumContext);
    //                this.Coder.RangeEncodeSymbol();
    //            }
    //            else
    //            {
    //                this.EncodeBinarySymbol(symbol, this.minimumContext);
    //                this.Coder.RangeShiftEncodeSymbol(14);
    //            }
    //            while (this.foundState == PpmState.Zero)
    //            {
    //                this.Coder.RangeEncoderNormalize(target);
    //                do
    //                {
    //                    this.orderFall++;
    //                    this.minimumContext = this.minimumContext.Suffix;
    //                    if (this.minimumContext == PpmContext.Zero)
    //                    {
    //                        this.Coder.RangeEncoderFlush(target);
    //                        return;
    //                    }
    //                }
    //                while (this.minimumContext.NumberStatistics == this.numberMasked);
    //                this.EncodeSymbol2(symbol, this.minimumContext);
    //                this.Coder.RangeEncodeSymbol();
    //            }
    //            if ((this.orderFall == 0) && (this.foundState.Successor >= this.Allocator.BaseUnit))
    //            {
    //                this.maximumContext = this.foundState.Successor;
    //            }
    //            else
    //            {
    //                this.UpdateModel(this.minimumContext);
    //                if (this.escapeCount == 0)
    //                {
    //                    this.ClearMask();
    //                }
    //            }
    //            this.Coder.RangeEncoderNormalize(target);
    //        }
    //    }

    //    internal SharpCompress.Compressor.PPMd.I1.Coder EncodeStart(PpmdProperties properties)
    //    {
    //        this.Allocator = properties.Allocator;
    //        this.Coder = new SharpCompress.Compressor.PPMd.I1.Coder();
    //        this.Coder.RangeEncoderInitialize();
    //        this.StartModel(properties.ModelOrder, properties.ModelRestorationMethod);
    //        return this.Coder;
    //    }

    //    private void EncodeSymbol1(int symbol, PpmContext context)
    //    {
    //        PpmState state2;
    //        uint numberStatistics = context.Statistics.Symbol;
    //        PpmState statistics = context.Statistics;
    //        this.Coder.Scale = context.SummaryFrequency;
    //        if (numberStatistics == symbol)
    //        {
    //            this.Coder.HighCount = statistics.Frequency;
    //            this.previousSuccess = ((2 * this.Coder.HighCount) >= this.Coder.Scale) ? ((byte) 1) : ((byte) 0);
    //            this.foundState = statistics;
    //            this.foundState.Frequency = (byte) (this.foundState.Frequency + 4);
    //            context.SummaryFrequency = (ushort) (context.SummaryFrequency + 4);
    //            this.runLength += this.previousSuccess;
    //            if (statistics.Frequency > 0x7c)
    //            {
    //                this.Rescale(context);
    //            }
    //            this.Coder.LowCount = 0;
    //            return;
    //        }
    //        uint frequency = statistics.Frequency;
    //        numberStatistics = context.NumberStatistics;
    //        this.previousSuccess = 0;
    //    Label_01A8:
    //        state2 = statistics = PpmState.op_Increment(statistics);
    //        if (state2.Symbol != symbol)
    //        {
    //            frequency += statistics.Frequency;
    //            if (--numberStatistics == 0)
    //            {
    //                this.Coder.LowCount = frequency;
    //                this.characterMask[statistics.Symbol] = this.escapeCount;
    //                this.numberMasked = context.NumberStatistics;
    //                numberStatistics = context.NumberStatistics;
    //                this.foundState = PpmState.Zero;
    //                do
    //                {
    //                    state2 = statistics = PpmState.op_Decrement(statistics);
    //                    this.characterMask[state2.Symbol] = this.escapeCount;
    //                }
    //                while (--numberStatistics != 0);
    //                this.Coder.HighCount = this.Coder.Scale;
    //                return;
    //            }
    //            goto Label_01A8;
    //        }
    //        this.Coder.HighCount = (this.Coder.LowCount = frequency) + statistics.Frequency;
    //        this.Update1(statistics, context);
    //    }

    //    private void EncodeSymbol2(int symbol, PpmContext context)
    //    {
    //        See2Context context2 = this.MakeEscapeFrequency(context);
    //        uint num2 = 0;
    //        uint num3 = (uint) (context.NumberStatistics - this.numberMasked);
    //        PpmState state = context.Statistics - 1;
    //        do
    //        {
    //            uint num;
    //            PpmState state3;
    //            do
    //            {
    //                state3 = state[1];
    //                num = state3.Symbol;
    //                state = PpmState.op_Increment(state);
    //            }
    //            while (this.characterMask[num] == this.escapeCount);
    //            this.characterMask[num] = this.escapeCount;
    //            if (num == symbol)
    //            {
    //                this.Coder.LowCount = num2;
    //                num2 += state.Frequency;
    //                this.Coder.HighCount = num2;
    //                PpmState state2 = state;
    //                while (--num3 != 0)
    //                {
    //                    do
    //                    {
    //                        state3 = state2[1];
    //                        num = state3.Symbol;
    //                        state2 = PpmState.op_Increment(state2);
    //                    }
    //                    while (this.characterMask[num] == this.escapeCount);
    //                    num2 += state2.Frequency;
    //                }
    //                this.Coder.Scale += num2;
    //                context2.Update();
    //                this.Update2(state, context);
    //                return;
    //            }
    //            num2 += state.Frequency;
    //        }
    //        while (--num3 != 0);
    //        this.Coder.LowCount = num2;
    //        this.Coder.Scale += this.Coder.LowCount;
    //        this.Coder.HighCount = this.Coder.Scale;
    //        context2.Summary = (ushort) (context2.Summary + ((ushort) this.Coder.Scale));
    //        this.numberMasked = context.NumberStatistics;
    //    }

    //    private See2Context MakeEscapeFrequency(PpmContext context)
    //    {
    //        See2Context context2;
    //        uint numberStatistics = (uint) (2 * context.NumberStatistics);
    //        if (context.NumberStatistics != 0xff)
    //        {
    //            numberStatistics = context.Suffix.NumberStatistics;
    //            int num2 = this.probabilities[context.NumberStatistics + 2] - 3;
    //            int num3 = (((context.SummaryFrequency > (11 * (context.NumberStatistics + 1))) ? 1 : 0) + (((2 * context.NumberStatistics) < (numberStatistics + this.numberMasked)) ? 2 : 0)) + context.Flags;
    //            context2 = this.see2Contexts[num2, num3];
    //            this.Coder.Scale = context2.Mean();
    //            return context2;
    //        }
    //        context2 = this.emptySee2Context;
    //        this.Coder.Scale = 1;
    //        return context2;
    //    }

    //    private static int Mean(int sum, int shift, int round)
    //    {
    //        return ((sum + (1 << ((shift - round) & 0x1f))) >> shift);
    //    }

    //    private PpmContext ReduceOrder(PpmState state, PpmContext context)
    //    {
    //        PpmState[] stateArray = new PpmState[0x10];
    //        uint num = 0;
    //        PpmContext context2 = context;
    //        PpmContext text = this.Allocator.Text;
    //        byte symbol = this.foundState.Symbol;
    //        stateArray[num++] = this.foundState;
    //        this.foundState.Successor = text;
    //        this.orderFall++;
    //        bool flag = false;
    //        if (state != PpmState.Zero)
    //        {
    //            context = context.Suffix;
    //            flag = true;
    //        }
    //        while (true)
    //        {
    //            if (flag)
    //            {
    //                flag = false;
    //            }
    //            else
    //            {
    //                if (context.Suffix == PpmContext.Zero)
    //                {
    //                    if (this.method > ModelRestorationMethod.Freeze)
    //                    {
    //                        do
    //                        {
    //                            stateArray[(int) ((IntPtr) (--num))].Successor = context;
    //                        }
    //                        while (num != 0);
    //                        this.Allocator.Text = this.Allocator.Heap + 1;
    //                        this.orderFall = 1;
    //                    }
    //                    return context;
    //                }
    //                context = context.Suffix;
    //                if (context.NumberStatistics != 0)
    //                {
    //                    byte num2;
    //                    state = context.Statistics;
    //                    if (state.Symbol != symbol)
    //                    {
    //                        do
    //                        {
    //                            PpmState state3 = state[1];
    //                            num2 = state3.Symbol;
    //                            state = PpmState.op_Increment(state);
    //                        }
    //                        while (num2 != symbol);
    //                    }
    //                    num2 = (state.Frequency < 0x73) ? ((byte) 2) : ((byte) 0);
    //                    state.Frequency = (byte) (state.Frequency + num2);
    //                    context.SummaryFrequency = (ushort) (context.SummaryFrequency + num2);
    //                }
    //                else
    //                {
    //                    state = context.FirstState;
    //                    state.Frequency = (byte) (state.Frequency + ((state.Frequency < 0x20) ? ((byte) 1) : ((byte) 0)));
    //                }
    //            }
    //            if (state.Successor != PpmContext.Zero)
    //            {
    //                if (this.method > ModelRestorationMethod.Freeze)
    //                {
    //                    context = state.Successor;
    //                    do
    //                    {
    //                        stateArray[(int) ((IntPtr) (--num))].Successor = context;
    //                    }
    //                    while (num != 0);
    //                    this.Allocator.Text = this.Allocator.Heap + 1;
    //                    this.orderFall = 1;
    //                    return context;
    //                }
    //                if (state.Successor <= text)
    //                {
    //                    PpmState foundState = this.foundState;
    //                    this.foundState = state;
    //                    state.Successor = this.CreateSuccessors(false, PpmState.Zero, context);
    //                    this.foundState = foundState;
    //                }
    //                if ((this.orderFall == 1) && (context2 == this.maximumContext))
    //                {
    //                    this.foundState.Successor = state.Successor;
    //                    this.Allocator.Text = Pointer.op_Decrement(this.Allocator.Text);
    //                }
    //                return state.Successor;
    //            }
    //            stateArray[num++] = state;
    //            state.Successor = text;
    //            this.orderFall++;
    //        }
    //    }

    //    private void Refresh(uint oldUnitCount, bool scale, PpmContext context)
    //    {
    //        int numberStatistics = context.NumberStatistics;
    //        int num3 = scale ? 1 : 0;
    //        context.Statistics = this.Allocator.ShrinkUnits(context.Statistics, oldUnitCount, (uint) ((numberStatistics + 2) >> 1));
    //        PpmState statistics = context.Statistics;
    //        context.Flags = (byte) ((context.Flags & (0x10 + (scale ? 4 : 0))) + ((statistics.Symbol >= 0x40) ? 8 : 0));
    //        int num2 = context.SummaryFrequency - statistics.Frequency;
    //        statistics.Frequency = (byte) ((statistics.Frequency + num3) >> num3);
    //        context.SummaryFrequency = statistics.Frequency;
    //        do
    //        {
    //            PpmState state2 = statistics = PpmState.op_Increment(statistics);
    //            num2 -= state2.Frequency;
    //            statistics.Frequency = (byte) ((statistics.Frequency + num3) >> num3);
    //            context.SummaryFrequency = (ushort) (context.SummaryFrequency + statistics.Frequency);
    //            context.Flags = (byte) (context.Flags | ((statistics.Symbol >= 0x40) ? ((byte) 8) : ((byte) 0)));
    //        }
    //        while (--numberStatistics != 0);
    //        num2 = (num2 + num3) >> num3;
    //        context.SummaryFrequency = (ushort) (context.SummaryFrequency + ((ushort) num2));
    //    }

    //    private PpmContext RemoveBinaryContexts(int order, PpmContext context)
    //    {
    //        PpmState firstState;
    //        if (context.NumberStatistics == 0)
    //        {
    //            firstState = context.FirstState;
    //            if ((firstState.Successor >= this.Allocator.BaseUnit) && (order < this.modelOrder))
    //            {
    //                firstState.Successor = this.RemoveBinaryContexts(order + 1, firstState.Successor);
    //            }
    //            else
    //            {
    //                firstState.Successor = PpmContext.Zero;
    //            }
    //            if ((firstState.Successor == PpmContext.Zero) && ((context.Suffix.NumberStatistics == 0) || (context.Suffix.Flags == 0xff)))
    //            {
    //                this.Allocator.FreeUnits(context, 1);
    //                return PpmContext.Zero;
    //            }
    //            return context;
    //        }
    //        for (firstState = context.Statistics + context.NumberStatistics; firstState >= context.Statistics; firstState = PpmState.op_Decrement(firstState))
    //        {
    //            if ((firstState.Successor >= this.Allocator.BaseUnit) && (order < this.modelOrder))
    //            {
    //                firstState.Successor = this.RemoveBinaryContexts(order + 1, firstState.Successor);
    //            }
    //            else
    //            {
    //                firstState.Successor = PpmContext.Zero;
    //            }
    //        }
    //        return context;
    //    }

    //    private void Rescale(PpmContext context)
    //    {
    //        byte symbol;
    //        byte frequency;
    //        PpmContext successor;
    //        PpmState state3;
    //        uint numberStatistics = context.NumberStatistics;
    //        PpmState foundState = this.foundState;
    //        while (foundState != context.Statistics)
    //        {
    //            Swap(foundState[0], foundState[-1]);
    //            foundState = PpmState.op_Decrement(foundState);
    //        }
    //        foundState.Frequency = (byte) (foundState.Frequency + 4);
    //        context.SummaryFrequency = (ushort) (context.SummaryFrequency + 4);
    //        uint num3 = (uint) (context.SummaryFrequency - foundState.Frequency);
    //        int num2 = ((this.orderFall != 0) || (this.method > ModelRestorationMethod.Freeze)) ? 1 : 0;
    //        foundState.Frequency = (byte) ((foundState.Frequency + num2) >> 1);
    //        context.SummaryFrequency = foundState.Frequency;
    //        do
    //        {
    //            state3 = foundState = PpmState.op_Increment(foundState);
    //            num3 -= state3.Frequency;
    //            foundState.Frequency = (byte) ((foundState.Frequency + num2) >> 1);
    //            context.SummaryFrequency = (ushort) (context.SummaryFrequency + foundState.Frequency);
    //            state3 = foundState[0];
    //            state3 = foundState[-1];
    //            if (state3.Frequency > state3.Frequency)
    //            {
    //                PpmState state = foundState;
    //                symbol = state.Symbol;
    //                frequency = state.Frequency;
    //                successor = state.Successor;
    //                do
    //                {
    //                    Copy(state[0], state[-1]);
    //                    state3 = state = PpmState.op_Decrement(state);
    //                    state3 = state3[-1];
    //                }
    //                while (frequency > state3.Frequency);
    //                state.Symbol = symbol;
    //                state.Frequency = frequency;
    //                state.Successor = successor;
    //            }
    //        }
    //        while (--numberStatistics != 0);
    //        if (foundState.Frequency == 0)
    //        {
    //            do
    //            {
    //                numberStatistics++;
    //                state3 = foundState = PpmState.op_Decrement(foundState);
    //            }
    //            while (state3.Frequency == 0);
    //            num3 += numberStatistics;
    //            uint unitCount = (uint) ((context.NumberStatistics + 2) >> 1);
    //            context.NumberStatistics = (byte) (context.NumberStatistics - ((byte) numberStatistics));
    //            if (context.NumberStatistics == 0)
    //            {
    //                symbol = context.Statistics.Symbol;
    //                frequency = context.Statistics.Frequency;
    //                successor = context.Statistics.Successor;
    //                frequency = (byte) ((((2 * frequency) + num3) - ((ulong) 1L)) / ((ulong) num3));
    //                if (frequency > 0x29)
    //                {
    //                    frequency = 0x29;
    //                }
    //                this.Allocator.FreeUnits(context.Statistics, unitCount);
    //                context.FirstStateSymbol = symbol;
    //                context.FirstStateFrequency = frequency;
    //                context.FirstStateSuccessor = successor;
    //                context.Flags = (byte) ((context.Flags & 0x10) + ((symbol >= 0x40) ? 8 : 0));
    //                this.foundState = context.FirstState;
    //                return;
    //            }
    //            context.Statistics = this.Allocator.ShrinkUnits(context.Statistics, unitCount, (uint) ((context.NumberStatistics + 2) >> 1));
    //            context.Flags = (byte) (context.Flags & 0xf7);
    //            numberStatistics = context.NumberStatistics;
    //            foundState = context.Statistics;
    //            context.Flags = (byte) (context.Flags | ((foundState.Symbol >= 0x40) ? ((byte) 8) : ((byte) 0)));
    //            do
    //            {
    //                state3 = foundState = PpmState.op_Increment(foundState);
    //                context.Flags = (byte) (context.Flags | ((state3.Symbol >= 0x40) ? ((byte) 8) : ((byte) 0)));
    //            }
    //            while (--numberStatistics != 0);
    //        }
    //        num3 -= num3 >> 1;
    //        context.SummaryFrequency = (ushort) (context.SummaryFrequency + ((ushort) num3));
    //        context.Flags = (byte) (context.Flags | 4);
    //        this.foundState = context.Statistics;
    //    }

    //    private void RestoreModel(PpmContext context, PpmContext minimumContext, PpmContext foundStateSuccessor)
    //    {
    //        this.Allocator.Text = this.Allocator.Heap;
    //        PpmContext maximumContext = this.maximumContext;
    //        while (maximumContext != context)
    //        {
    //            if ((maximumContext.NumberStatistics = (byte) (maximumContext.NumberStatistics - 1)) == 0)
    //            {
    //                maximumContext.Flags = (byte) ((maximumContext.Flags & 0x10) + ((maximumContext.Statistics.Symbol >= 0x40) ? 8 : 0));
    //                PpmState statistics = maximumContext.Statistics;
    //                Copy(maximumContext.FirstState, statistics);
    //                this.Allocator.SpecialFreeUnits(statistics);
    //                maximumContext.FirstStateFrequency = (byte) ((maximumContext.FirstStateFrequency + 11) >> 3);
    //            }
    //            else
    //            {
    //                this.Refresh((uint) ((maximumContext.NumberStatistics + 3) >> 1), false, maximumContext);
    //            }
    //            maximumContext = maximumContext.Suffix;
    //        }
    //        while (maximumContext != minimumContext)
    //        {
    //            if (maximumContext.NumberStatistics == 0)
    //            {
    //                maximumContext.FirstStateFrequency = (byte) (maximumContext.FirstStateFrequency - ((byte) (maximumContext.FirstStateFrequency >> 1)));
    //            }
    //            else if ((maximumContext.SummaryFrequency = (ushort) (maximumContext.SummaryFrequency + 4)) > (0x80 + (4 * maximumContext.NumberStatistics)))
    //            {
    //                this.Refresh((uint) ((maximumContext.NumberStatistics + 2) >> 1), true, maximumContext);
    //            }
    //            maximumContext = maximumContext.Suffix;
    //        }
    //        if (this.method > ModelRestorationMethod.Freeze)
    //        {
    //            this.maximumContext = foundStateSuccessor;
    //            this.Allocator.GlueCount += ((this.Allocator.MemoryNodes[1].Stamp & 1) == 0) ? 1 : 0;
    //        }
    //        else if (this.method == ModelRestorationMethod.Freeze)
    //        {
    //            while (this.maximumContext.Suffix != PpmContext.Zero)
    //            {
    //                this.maximumContext = this.maximumContext.Suffix;
    //            }
    //            this.RemoveBinaryContexts(0, this.maximumContext);
    //            this.method += 1;
    //            this.Allocator.GlueCount = 0;
    //            this.orderFall = this.modelOrder;
    //        }
    //        else if ((this.method == ModelRestorationMethod.Restart) || (this.Allocator.GetMemoryUsed() < (this.Allocator.AllocatorSize >> 1)))
    //        {
    //            this.StartModel(this.modelOrder, this.method);
    //            this.escapeCount = 0;
    //        }
    //        else
    //        {
    //            while (this.maximumContext.Suffix != PpmContext.Zero)
    //            {
    //                this.maximumContext = this.maximumContext.Suffix;
    //            }
    //            do
    //            {
    //                this.CutOff(0, this.maximumContext);
    //                this.Allocator.ExpandText();
    //            }
    //            while (this.Allocator.GetMemoryUsed() > (3 * (this.Allocator.AllocatorSize >> 2)));
    //            this.Allocator.GlueCount = 0;
    //            this.orderFall = this.modelOrder;
    //        }
    //    }

    //    private void StartModel(int modelOrder, ModelRestorationMethod modelRestorationMethod)
    //    {
    //        Array.Clear(this.characterMask, 0, this.characterMask.Length);
    //        this.escapeCount = 1;
    //        if (modelOrder < 2)
    //        {
    //            this.orderFall = this.modelOrder;
    //            for (PpmContext context = this.maximumContext; context.Suffix != PpmContext.Zero; context = context.Suffix)
    //            {
    //                this.orderFall--;
    //            }
    //        }
    //        else
    //        {
    //            int num4;
    //            this.modelOrder = modelOrder;
    //            this.orderFall = modelOrder;
    //            this.method = modelRestorationMethod;
    //            this.Allocator.Initialize();
    //            this.initialRunLength = -((modelOrder < 12) ? modelOrder : 12) - 1;
    //            this.runLength = this.initialRunLength;
    //            this.maximumContext = this.Allocator.AllocateContext();
    //            this.maximumContext.Suffix = PpmContext.Zero;
    //            this.maximumContext.NumberStatistics = 0xff;
    //            this.maximumContext.SummaryFrequency = (ushort) (this.maximumContext.NumberStatistics + 2);
    //            this.maximumContext.Statistics = this.Allocator.AllocateUnits(0x80);
    //            this.previousSuccess = 0;
    //            for (int i = 0; i < 0x100; i++)
    //            {
    //                PpmState state = this.maximumContext.Statistics[i];
    //                state.Symbol = (byte) i;
    //                state.Frequency = 1;
    //                state.Successor = PpmContext.Zero;
    //            }
    //            uint num2 = 0;
    //            int index = 0;
    //            while (num2 < 0x19)
    //            {
    //                while (this.probabilities[index] == num2)
    //                {
    //                    index++;
    //                }
    //                num4 = 0;
    //                while (num4 < 8)
    //                {
    //                    this.binarySummary[(int) ((IntPtr) num2), (int) ((IntPtr) num4)] = (ushort) (0x4000L - (InitialBinaryEscapes[num4] / (index + 1)));
    //                    num4++;
    //                }
    //                num4 = 8;
    //                while (num4 < 0x40)
    //                {
    //                    for (int j = 0; j < 8; j++)
    //                    {
    //                        this.binarySummary[(int) ((IntPtr) num2), (int) ((IntPtr) (num4 + j))] = this.binarySummary[(int) ((IntPtr) num2), (int) ((IntPtr) j)];
    //                    }
    //                    num4 += 8;
    //                }
    //                num2++;
    //            }
    //            num2 = 0;
    //            uint num6 = 0;
    //            while (num2 < 0x18)
    //            {
    //                while (this.probabilities[(int) ((IntPtr) (num6 + 3))] == (num2 + 3))
    //                {
    //                    num6++;
    //                }
    //                for (num4 = 0; num4 < 0x20; num4++)
    //                {
    //                    this.see2Contexts[(int) ((IntPtr) num2), (int) ((IntPtr) num4)].Initialize((2 * num6) + 5);
    //                }
    //                num2++;
    //            }
    //        }
    //    }

    //    private static void Swap(PpmState state1, PpmState state2)
    //    {
    //        byte symbol = state1.Symbol;
    //        byte frequency = state1.Frequency;
    //        PpmContext successor = state1.Successor;
    //        state1.Symbol = state2.Symbol;
    //        state1.Frequency = state2.Frequency;
    //        state1.Successor = state2.Successor;
    //        state2.Symbol = symbol;
    //        state2.Frequency = frequency;
    //        state2.Successor = successor;
    //    }

    //    private void Update1(PpmState state, PpmContext context)
    //    {
    //        this.foundState = state;
    //        this.foundState.Frequency = (byte) (this.foundState.Frequency + 4);
    //        context.SummaryFrequency = (ushort) (context.SummaryFrequency + 4);
    //        PpmState state2 = state[0];
    //        state2 = state[-1];
    //        if (state2.Frequency > state2.Frequency)
    //        {
    //            Swap(state[0], state[-1]);
    //            this.foundState = state = PpmState.op_Decrement(state);
    //            if (state.Frequency > 0x7c)
    //            {
    //                this.Rescale(context);
    //            }
    //        }
    //    }

    //    private void Update2(PpmState state, PpmContext context)
    //    {
    //        this.foundState = state;
    //        this.foundState.Frequency = (byte) (this.foundState.Frequency + 4);
    //        context.SummaryFrequency = (ushort) (context.SummaryFrequency + 4);
    //        if (state.Frequency > 0x7c)
    //        {
    //            this.Rescale(context);
    //        }
    //        this.escapeCount = (byte) (this.escapeCount + 1);
    //        this.runLength = this.initialRunLength;
    //    }

    //    private void UpdateModel(PpmContext minimumContext)
    //    {
    //        uint num3;
    //        PpmState zero = PpmState.Zero;
    //        PpmContext maximumContext = this.maximumContext;
    //        uint frequency = this.foundState.Frequency;
    //        byte symbol = this.foundState.Symbol;
    //        PpmContext successor = this.foundState.Successor;
    //        PpmContext suffix = minimumContext.Suffix;
    //        if ((frequency < 0x1f) && (suffix != PpmContext.Zero))
    //        {
    //            if (suffix.NumberStatistics != 0)
    //            {
    //                zero = suffix.Statistics;
    //                if (zero.Symbol != symbol)
    //                {
    //                    byte num8;
    //                    PpmState state2;
    //                    do
    //                    {
    //                        state2 = zero[1];
    //                        num8 = state2.Symbol;
    //                        zero = PpmState.op_Increment(zero);
    //                    }
    //                    while (num8 != symbol);
    //                    state2 = zero[0];
    //                    state2 = zero[-1];
    //                    if (state2.Frequency >= state2.Frequency)
    //                    {
    //                        Swap(zero[0], zero[-1]);
    //                        zero = PpmState.op_Decrement(zero);
    //                    }
    //                }
    //                num3 = (zero.Frequency < 0x73) ? 2 : 0;
    //                zero.Frequency = (byte) (zero.Frequency + ((byte) num3));
    //                suffix.SummaryFrequency = (ushort) (suffix.SummaryFrequency + ((byte) num3));
    //            }
    //            else
    //            {
    //                zero = suffix.FirstState;
    //                zero.Frequency = (byte) (zero.Frequency + ((zero.Frequency < 0x20) ? ((byte) 1) : ((byte) 0)));
    //            }
    //        }
    //        if ((this.orderFall == 0) && (successor != PpmContext.Zero))
    //        {
    //            this.foundState.Successor = this.CreateSuccessors(true, zero, minimumContext);
    //            if (this.foundState.Successor != PpmContext.Zero)
    //            {
    //                this.maximumContext = this.foundState.Successor;
    //                return;
    //            }
    //        }
    //        else
    //        {
    //            this.Allocator.Text[0] = symbol;
    //            this.Allocator.Text = Pointer.op_Increment(this.Allocator.Text);
    //            PpmContext text = this.Allocator.Text;
    //            if (this.Allocator.Text < this.Allocator.BaseUnit)
    //            {
    //                if (successor != PpmContext.Zero)
    //                {
    //                    if (successor < this.Allocator.BaseUnit)
    //                    {
    //                        successor = this.CreateSuccessors(false, zero, minimumContext);
    //                    }
    //                }
    //                else
    //                {
    //                    successor = this.ReduceOrder(zero, minimumContext);
    //                }
    //                if (successor != PpmContext.Zero)
    //                {
    //                    if (--this.orderFall == 0)
    //                    {
    //                        text = successor;
    //                        this.Allocator.Text -= (this.maximumContext != minimumContext) ? 1 : 0;
    //                    }
    //                    else if (this.method > ModelRestorationMethod.Freeze)
    //                    {
    //                        text = successor;
    //                        this.Allocator.Text = this.Allocator.Heap;
    //                        this.orderFall = 0;
    //                    }
    //                    uint numberStatistics = minimumContext.NumberStatistics;
    //                    uint num5 = (minimumContext.SummaryFrequency - numberStatistics) - frequency;
    //                    byte num9 = (symbol >= 0x40) ? ((byte) 8) : ((byte) 0);
    //                    while (maximumContext != minimumContext)
    //                    {
    //                        byte num11;
    //                        uint num2 = maximumContext.NumberStatistics;
    //                        if (num2 != 0)
    //                        {
    //                            if ((num2 & 1) != 0)
    //                            {
    //                                zero = this.Allocator.ExpandUnits(maximumContext.Statistics, (uint) ((num2 + 1) >> 1));
    //                                if (zero == PpmState.Zero)
    //                                {
    //                                    goto Label_059D;
    //                                }
    //                                maximumContext.Statistics = zero;
    //                            }
    //                            maximumContext.SummaryFrequency = (ushort) (maximumContext.SummaryFrequency + ((((3 * num2) + 1) < numberStatistics) ? ((ushort) 1) : ((ushort) 0)));
    //                        }
    //                        else
    //                        {
    //                            zero = this.Allocator.AllocateUnits(1);
    //                            if (zero == PpmState.Zero)
    //                            {
    //                                goto Label_059D;
    //                            }
    //                            Copy(zero, maximumContext.FirstState);
    //                            maximumContext.Statistics = zero;
    //                            if (zero.Frequency < 30)
    //                            {
    //                                zero.Frequency = (byte) (zero.Frequency + zero.Frequency);
    //                            }
    //                            else
    //                            {
    //                                zero.Frequency = 120;
    //                            }
    //                            maximumContext.SummaryFrequency = (ushort) ((zero.Frequency + this.initialEscape) + ((numberStatistics > 2) ? 1 : 0));
    //                        }
    //                        num3 = (uint) ((2 * frequency) * (maximumContext.SummaryFrequency + 6));
    //                        uint num4 = num5 + maximumContext.SummaryFrequency;
    //                        if (num3 < (6 * num4))
    //                        {
    //                            num3 = (uint) ((1 + ((num3 > num4) ? 1 : 0)) + ((num3 >= (4 * num4)) ? 1 : 0));
    //                            maximumContext.SummaryFrequency = (ushort) (maximumContext.SummaryFrequency + 4);
    //                        }
    //                        else
    //                        {
    //                            num3 = (uint) (((4 + ((num3 > (9 * num4)) ? 1 : 0)) + ((num3 > (12 * num4)) ? 1 : 0)) + ((num3 > (15 * num4)) ? 1 : 0));
    //                            maximumContext.SummaryFrequency = (ushort) (maximumContext.SummaryFrequency + ((ushort) num3));
    //                        }
    //                        maximumContext.NumberStatistics = num11 = (byte) (maximumContext.NumberStatistics + 1);
    //                        zero = maximumContext.Statistics + num11;
    //                        zero.Successor = text;
    //                        zero.Symbol = symbol;
    //                        zero.Frequency = (byte) num3;
    //                        maximumContext.Flags = (byte) (maximumContext.Flags | num9);
    //                        maximumContext = maximumContext.Suffix;
    //                    }
    //                    this.maximumContext = successor;
    //                    return;
    //                }
    //            }
    //        }
    //    Label_059D:
    //        this.RestoreModel(maximumContext, minimumContext, successor);
    //    }

    //    [StructLayout(LayoutKind.Sequential)]
    //    internal struct PpmContext
    //    {
    //        public const int Size = 12;
    //        public uint Address;
    //        public byte[] Memory;
    //        public static readonly Model.PpmContext Zero;
    //        public PpmContext(uint address, byte[] memory)
    //        {
    //            this.Address = address;
    //            this.Memory = memory;
    //        }

    //        public byte NumberStatistics
    //        {
    //            get
    //            {
    //                return this.Memory[this.Address];
    //            }
    //            set
    //            {
    //                this.Memory[this.Address] = value;
    //            }
    //        }
    //        public byte Flags
    //        {
    //            get
    //            {
    //                return this.Memory[(int) ((IntPtr) (this.Address + 1))];
    //            }
    //            set
    //            {
    //                this.Memory[(int) ((IntPtr) (this.Address + 1))] = value;
    //            }
    //        }
    //        public ushort SummaryFrequency
    //        {
    //            get
    //            {
    //                return (ushort) (this.Memory[(int) ((IntPtr) (this.Address + 2))] | (this.Memory[(int) ((IntPtr) (this.Address + 3))] << 8));
    //            }
    //            set
    //            {
    //                this.Memory[(int) ((IntPtr) (this.Address + 2))] = (byte) value;
    //                this.Memory[(int) ((IntPtr) (this.Address + 3))] = (byte) (value >> 8);
    //            }
    //        }
    //        public PpmState Statistics
    //        {
    //            get
    //            {
    //                return new PpmState((uint) (((this.Memory[(int) ((IntPtr) (this.Address + 4))] | (this.Memory[(int) ((IntPtr) (this.Address + 5))] << 8)) | (this.Memory[(int) ((IntPtr) (this.Address + 6))] << 0x10)) | (this.Memory[(int) ((IntPtr) (this.Address + 7))] << 0x18)), this.Memory);
    //            }
    //            set
    //            {
    //                this.Memory[(int) ((IntPtr) (this.Address + 4))] = (byte) value.Address;
    //                this.Memory[(int) ((IntPtr) (this.Address + 5))] = (byte) (value.Address >> 8);
    //                this.Memory[(int) ((IntPtr) (this.Address + 6))] = (byte) (value.Address >> 0x10);
    //                this.Memory[(int) ((IntPtr) (this.Address + 7))] = (byte) (value.Address >> 0x18);
    //            }
    //        }
    //        public Model.PpmContext Suffix
    //        {
    //            get
    //            {
    //                return new Model.PpmContext((uint) (((this.Memory[(int) ((IntPtr) (this.Address + 8))] | (this.Memory[(int) ((IntPtr) (this.Address + 9))] << 8)) | (this.Memory[(int) ((IntPtr) (this.Address + 10))] << 0x10)) | (this.Memory[(int) ((IntPtr) (this.Address + 11))] << 0x18)), this.Memory);
    //            }
    //            set
    //            {
    //                this.Memory[(int) ((IntPtr) (this.Address + 8))] = (byte) value.Address;
    //                this.Memory[(int) ((IntPtr) (this.Address + 9))] = (byte) (value.Address >> 8);
    //                this.Memory[(int) ((IntPtr) (this.Address + 10))] = (byte) (value.Address >> 0x10);
    //                this.Memory[(int) ((IntPtr) (this.Address + 11))] = (byte) (value.Address >> 0x18);
    //            }
    //        }
    //        public PpmState FirstState
    //        {
    //            get
    //            {
    //                return new PpmState(this.Address + 2, this.Memory);
    //            }
    //        }
    //        public byte FirstStateSymbol
    //        {
    //            get
    //            {
    //                return this.Memory[(int) ((IntPtr) (this.Address + 2))];
    //            }
    //            set
    //            {
    //                this.Memory[(int) ((IntPtr) (this.Address + 2))] = value;
    //            }
    //        }
    //        public byte FirstStateFrequency
    //        {
    //            get
    //            {
    //                return this.Memory[(int) ((IntPtr) (this.Address + 3))];
    //            }
    //            set
    //            {
    //                this.Memory[(int) ((IntPtr) (this.Address + 3))] = value;
    //            }
    //        }
    //        public Model.PpmContext FirstStateSuccessor
    //        {
    //            get
    //            {
    //                return new Model.PpmContext((uint) (((this.Memory[(int) ((IntPtr) (this.Address + 4))] | (this.Memory[(int) ((IntPtr) (this.Address + 5))] << 8)) | (this.Memory[(int) ((IntPtr) (this.Address + 6))] << 0x10)) | (this.Memory[(int) ((IntPtr) (this.Address + 7))] << 0x18)), this.Memory);
    //            }
    //            set
    //            {
    //                this.Memory[(int) ((IntPtr) (this.Address + 4))] = (byte) value.Address;
    //                this.Memory[(int) ((IntPtr) (this.Address + 5))] = (byte) (value.Address >> 8);
    //                this.Memory[(int) ((IntPtr) (this.Address + 6))] = (byte) (value.Address >> 0x10);
    //                this.Memory[(int) ((IntPtr) (this.Address + 7))] = (byte) (value.Address >> 0x18);
    //            }
    //        }
    //        public static implicit operator Model.PpmContext(Pointer pointer)
    //        {
    //            return new Model.PpmContext(pointer.Address, pointer.Memory);
    //        }

    //        public static Model.PpmContext operator +(Model.PpmContext context, int offset)
    //        {
    //            context.Address += (uint) (offset * 12);
    //            return context;
    //        }

    //        public static Model.PpmContext operator -(Model.PpmContext context, int offset)
    //        {
    //            context.Address -= (uint) (offset * 12);
    //            return context;
    //        }

    //        public static bool operator <=(Model.PpmContext context1, Model.PpmContext context2)
    //        {
    //            return (context1.Address <= context2.Address);
    //        }

    //        public static bool operator >=(Model.PpmContext context1, Model.PpmContext context2)
    //        {
    //            return (context1.Address >= context2.Address);
    //        }

    //        public static bool operator ==(Model.PpmContext context1, Model.PpmContext context2)
    //        {
    //            return (context1.Address == context2.Address);
    //        }

    //        public static bool operator !=(Model.PpmContext context1, Model.PpmContext context2)
    //        {
    //            return (context1.Address != context2.Address);
    //        }

    //        public override bool Equals(object obj)
    //        {
    //            if (obj is Model.PpmContext)
    //            {
    //                Model.PpmContext context = (Model.PpmContext) obj;
    //                return (context.Address == this.Address);
    //            }
    //            return base.Equals(obj);
    //        }

    //        public override int GetHashCode()
    //        {
    //            return this.Address.GetHashCode();
    //        }

    //        static PpmContext()
    //        {
    //            Zero = new Model.PpmContext(0, null);
    //        }
    //    }
    //}
    /// <summary>
    /// The model.
    /// </summary>
    internal partial class Model {
        public const uint Signature = 0x84acaf8fU;
        public const char Variant = 'I';
        public const int MaximumOrder = 16; // maximum allowed model order

        private const byte UpperFrequency = 5;
        private const byte IntervalBitCount = 7;
        private const byte PeriodBitCount = 7;
        private const byte TotalBitCount = IntervalBitCount + PeriodBitCount;
        private const uint Interval = 1 << IntervalBitCount;
        private const uint BinaryScale = 1 << TotalBitCount;
        private const uint MaximumFrequency = 124;
        private const uint OrderBound = 9;

        private See2Context[,] see2Contexts;
        private See2Context emptySee2Context;
        private PpmContext maximumContext;
        private ushort[,] binarySummary = new ushort[25, 64]; // binary SEE-contexts
        private byte[] numberStatisticsToBinarySummaryIndex = new byte[256];
        private byte[] probabilities = new byte[260];
        private byte[] characterMask = new byte[256];
        private byte escapeCount;
        private int modelOrder;
        private int orderFall;
        private int initialEscape;
        private int initialRunLength;
        private int runLength;
        private byte previousSuccess;
        private byte numberMasked;
        private ModelRestorationMethod method;
        private PpmState foundState; // found next state transition

        private Allocator Allocator;
        private Coder Coder;
        private PpmContext minimumContext;
        private byte numberStatistics;
        private PpmState[] decodeStates = new PpmState[256];

        private static readonly ushort[] InitialBinaryEscapes =
            {
                0x3CDD, 0x1F3F, 0x59BF, 0x48F3, 0x64A1, 0x5ABC, 0x6632,
                0x6051
            };

        private static readonly byte[] ExponentialEscapes = { 25, 14, 9, 7, 5, 5, 4, 4, 4, 3, 3, 3, 2, 2, 2, 2 };

        #region Public Methods

        public Model() {
            // Construct the conversion table for number statistics.  Initially it will contain the following values.
            //
            // 0 2 4 4 4 4 4 4 4 4 4 6 6 6 6 6 6 6 6 6 6 6 6 6 6 6 6 6 6 6 6 6
            // 6 6 6 6 6 6 6 6 6 6 6 6 6 6 6 6 6 6 6 6 6 6 6 6 6 6 6 6 6 6 6 6
            // 6 6 6 6 6 6 6 6 6 6 6 6 6 6 6 6 6 6 6 6 6 6 6 6 6 6 6 6 6 6 6 6
            // 6 6 6 6 6 6 6 6 6 6 6 6 6 6 6 6 6 6 6 6 6 6 6 6 6 6 6 6 6 6 6 6
            // 6 6 6 6 6 6 6 6 6 6 6 6 6 6 6 6 6 6 6 6 6 6 6 6 6 6 6 6 6 6 6 6
            // 6 6 6 6 6 6 6 6 6 6 6 6 6 6 6 6 6 6 6 6 6 6 6 6 6 6 6 6 6 6 6 6
            // 6 6 6 6 6 6 6 6 6 6 6 6 6 6 6 6 6 6 6 6 6 6 6 6 6 6 6 6 6 6 6 6
            // 6 6 6 6 6 6 6 6 6 6 6 6 6 6 6 6 6 6 6 6 6 6 6 6 6 6 6 6 6 6 6 6

            numberStatisticsToBinarySummaryIndex[0] = 2 * 0;
            numberStatisticsToBinarySummaryIndex[1] = 2 * 1;
            for (int index = 2; index < 11; index++)
                numberStatisticsToBinarySummaryIndex[index] = 2 * 2;
            for (int index = 11; index < 256; index++)
                numberStatisticsToBinarySummaryIndex[index] = 2 * 3;

            // Construct the probability table.  Initially it will contain the following values (depending on the value of
            // the upper frequency).
            //
            // 00 01 02 03 04 05 06 06 07 07 07 08 08 08 08 09 09 09 09 09 10 10 10 10 10 10 11 11 11 11 11 11
            // 11 12 12 12 12 12 12 12 12 13 13 13 13 13 13 13 13 13 14 14 14 14 14 14 14 14 14 14 15 15 15 15
            // 15 15 15 15 15 15 15 16 16 16 16 16 16 16 16 16 16 16 16 17 17 17 17 17 17 17 17 17 17 17 17 17
            // 18 18 18 18 18 18 18 18 18 18 18 18 18 18 19 19 19 19 19 19 19 19 19 19 19 19 19 19 19 20 20 20
            // 20 20 20 20 20 20 20 20 20 20 20 20 20 21 21 21 21 21 21 21 21 21 21 21 21 21 21 21 21 21 22 22
            // 22 22 22 22 22 22 22 22 22 22 22 22 22 22 22 22 23 23 23 23 23 23 23 23 23 23 23 23 23 23 23 23
            // 23 23 23 24 24 24 24 24 24 24 24 24 24 24 24 24 24 24 24 24 24 24 24 25 25 25 25 25 25 25 25 25
            // 25 25 25 25 25 25 25 25 25 25 25 25 26 26 26 26 26 26 26 26 26 26 26 26 26 26 26 26 26 26 26 26
            // 26 26 27 27

            uint count = 1;
            uint step = 1;
            uint probability = UpperFrequency;

            for (int index = 0; index < UpperFrequency; index++)
                probabilities[index] = (byte)index;

            for (int index = UpperFrequency; index < 260; index++) {
                probabilities[index] = (byte)probability;
                count--;
                if (count == 0) {
                    step++;
                    count = step;
                    probability++;
                }
            }

            // Create the context array.

            see2Contexts = new See2Context[24, 32];
            for (int index1 = 0; index1 < 24; index1++)
                for (int index2 = 0; index2 < 32; index2++)
                    see2Contexts[index1, index2] = new See2Context();

            // Set the signature (identifying the algorithm).

            emptySee2Context = new See2Context();
            emptySee2Context.Summary = (ushort)(Signature & 0x0000ffff);
            emptySee2Context.Shift = (byte)((Signature >> 16) & 0x000000ff);
            emptySee2Context.Count = (byte)(Signature >> 24);
        }

        /// <summary>
        /// Encode (ie. compress) a given source stream, writing the encoded result to the target stream.
        /// </summary>
        public void Encode(Stream target, Stream source, PpmdProperties properties) {
            if (target == null)
                throw new ArgumentNullException("target");

            if (source == null)
                throw new ArgumentNullException("source");

            EncodeStart(properties);
            EncodeBlock(target, source, true);
        }

        internal Coder EncodeStart(PpmdProperties properties) {
            Allocator = properties.Allocator;
            Coder = new Coder();
            Coder.RangeEncoderInitialize();
            StartModel(properties.ModelOrder, properties.ModelRestorationMethod);
            return Coder;
        }

        internal void EncodeBlock(Stream target, Stream source, bool final) {
            while (true) {
                minimumContext = maximumContext;
                numberStatistics = minimumContext.NumberStatistics;

                int c = source.ReadByte();
                if (c < 0 && !final)
                    return;

                if (numberStatistics != 0) {
                    EncodeSymbol1(c, minimumContext);
                    Coder.RangeEncodeSymbol();
                }
                else {
                    EncodeBinarySymbol(c, minimumContext);
                    Coder.RangeShiftEncodeSymbol(TotalBitCount);
                }

                while (foundState == PpmState.Zero) {
                    Coder.RangeEncoderNormalize(target);
                    do {
                        orderFall++;
                        minimumContext = minimumContext.Suffix;
                        if (minimumContext == PpmContext.Zero)
                            goto StopEncoding;
                    } while (minimumContext.NumberStatistics == numberMasked);
                    EncodeSymbol2(c, minimumContext);
                    Coder.RangeEncodeSymbol();
                }

                if (orderFall == 0 && (Pointer)foundState.Successor >= Allocator.BaseUnit) {
                    maximumContext = foundState.Successor;
                }
                else {
                    UpdateModel(minimumContext);
                    if (escapeCount == 0)
                        ClearMask();
                }

                Coder.RangeEncoderNormalize(target);
            }

        StopEncoding:
            Coder.RangeEncoderFlush(target);
        }


        /// <summary>
        /// Dencode (ie. decompress) a given source stream, writing the decoded result to the target stream.
        /// </summary>
        public void Decode(Stream target, Stream source, PpmdProperties properties) {
            if (target == null)
                throw new ArgumentNullException("target");

            if (source == null)
                throw new ArgumentNullException("source");

            DecodeStart(source, properties);
            byte[] buffer = new byte[65536];
            int read;
            while ((read = DecodeBlock(source, buffer, 0, buffer.Length)) != 0)
                target.Write(buffer, 0, read);

            return;
        }

        internal Coder DecodeStart(Stream source, PpmdProperties properties) {
            Allocator = properties.Allocator;
            Coder = new Coder();
            Coder.RangeDecoderInitialize(source);
            StartModel(properties.ModelOrder, properties.ModelRestorationMethod);
            minimumContext = maximumContext;
            numberStatistics = minimumContext.NumberStatistics;
            return Coder;
        }

        internal int DecodeBlock(Stream source, byte[] buffer, int offset, int count) {
            if (minimumContext == PpmContext.Zero)
                return 0;

            int total = 0;
            while (total < count) {
                if (numberStatistics != 0)
                    DecodeSymbol1(minimumContext);
                else
                    DecodeBinarySymbol(minimumContext);

                Coder.RangeRemoveSubrange();

                while (foundState == PpmState.Zero) {
                    Coder.RangeDecoderNormalize(source);
                    do {
                        orderFall++;
                        minimumContext = minimumContext.Suffix;
                        if (minimumContext == PpmContext.Zero)
                            goto StopDecoding;
                    } while (minimumContext.NumberStatistics == numberMasked);
                    DecodeSymbol2(minimumContext);
                    Coder.RangeRemoveSubrange();
                }

                buffer[offset] = foundState.Symbol;
                offset++;
                total++;

                if (orderFall == 0 && (Pointer)foundState.Successor >= Allocator.BaseUnit) {
                    maximumContext = foundState.Successor;
                }
                else {
                    UpdateModel(minimumContext);
                    if (escapeCount == 0)
                        ClearMask();
                }

                minimumContext = maximumContext;
                numberStatistics = minimumContext.NumberStatistics;
                Coder.RangeDecoderNormalize(source);
            }

        StopDecoding:
            return total;
        }

        #endregion

        #region Private Methods

        /// <summary>
        /// Initialise the model (unless the model order is set to 1 in which case the model should be cleared so that
        /// the statistics are carried over, allowing "solid" mode compression).
        /// </summary>
        private void StartModel(int modelOrder, ModelRestorationMethod modelRestorationMethod) {
            Array.Clear(characterMask, 0, characterMask.Length);
            escapeCount = 1;

            // Compress in "solid" mode if the model order value is set to 1 (this will examine the current PPM context
            // structures to determine the value of orderFall).

            if (modelOrder < 2) {
                orderFall = this.modelOrder;
                for (PpmContext context = maximumContext; context.Suffix != PpmContext.Zero; context = context.Suffix)
                    orderFall--;
                return;
            }

            this.modelOrder = modelOrder;
            orderFall = modelOrder;
            method = modelRestorationMethod;
            Allocator.Initialize();
            initialRunLength = -((modelOrder < 12) ? modelOrder : 12) - 1;
            runLength = initialRunLength;

            // Allocate the context structure.

            maximumContext = Allocator.AllocateContext();
            maximumContext.Suffix = PpmContext.Zero;
            maximumContext.NumberStatistics = 255;
            maximumContext.SummaryFrequency = (ushort)(maximumContext.NumberStatistics + 2);
            maximumContext.Statistics = Allocator.AllocateUnits(256 / 2);
            // allocates enough space for 256 PPM states (each is 6 bytes)

            previousSuccess = 0;
            for (int index = 0; index < 256; index++) {
                PpmState state = maximumContext.Statistics[index];
                state.Symbol = (byte)index;
                state.Frequency = 1;
                state.Successor = PpmContext.Zero;
            }

            uint probability = 0;
            for (int index1 = 0; probability < 25; probability++) {
                while (probabilities[index1] == probability)
                    index1++;
                for (int index2 = 0; index2 < 8; index2++)
                    binarySummary[probability, index2] =
                        (ushort)(BinaryScale - InitialBinaryEscapes[index2] / (index1 + 1));
                for (int index2 = 8; index2 < 64; index2 += 8)
                    for (int index3 = 0; index3 < 8; index3++)
                        binarySummary[probability, index2 + index3] = binarySummary[probability, index3];
            }

            probability = 0;
            for (uint index1 = 0; probability < 24; probability++) {
                while (probabilities[index1 + 3] == probability + 3)
                    index1++;
                for (int index2 = 0; index2 < 32; index2++)
                    see2Contexts[probability, index2].Initialize(2 * index1 + 5);
            }
        }

        private void UpdateModel(PpmContext minimumContext) {
            PpmState state = PpmState.Zero;
            PpmContext Successor;
            PpmContext currentContext = maximumContext;
            uint numberStatistics;
            uint ns1;
            uint cf;
            uint sf;
            uint s0;
            uint foundStateFrequency = foundState.Frequency;
            byte foundStateSymbol = foundState.Symbol;
            byte symbol;
            byte flag;

            PpmContext foundStateSuccessor = foundState.Successor;
            PpmContext context = minimumContext.Suffix;

            if ((foundStateFrequency < MaximumFrequency / 4) && (context != PpmContext.Zero)) {
                if (context.NumberStatistics != 0) {
                    state = context.Statistics;
                    if (state.Symbol != foundStateSymbol) {
                        do {
                            symbol = state[1].Symbol;
                            state++;
                        } while (symbol != foundStateSymbol);
                        if (state[0].Frequency >= state[-1].Frequency) {
                            Swap(state[0], state[-1]);
                            state--;
                        }
                    }
                    cf = (uint)((state.Frequency < MaximumFrequency - 9) ? 2 : 0);
                    state.Frequency += (byte)cf;
                    context.SummaryFrequency += (byte)cf;
                }
                else {
                    state = context.FirstState;
                    state.Frequency += (byte)((state.Frequency < 32) ? 1 : 0);
                }
            }

            if (orderFall == 0 && foundStateSuccessor != PpmContext.Zero) {
                foundState.Successor = CreateSuccessors(true, state, minimumContext);
                if (foundState.Successor == PpmContext.Zero)
                    goto RestartModel;
                maximumContext = foundState.Successor;
                return;
            }

            Allocator.Text[0] = foundStateSymbol;
            Allocator.Text++;
            Successor = Allocator.Text;

            if (Allocator.Text >= Allocator.BaseUnit)
                goto RestartModel;

            if (foundStateSuccessor != PpmContext.Zero) {
                if (foundStateSuccessor < Allocator.BaseUnit)
                    foundStateSuccessor = CreateSuccessors(false, state, minimumContext);
            }
            else {
                foundStateSuccessor = ReduceOrder(state, minimumContext);
            }

            if (foundStateSuccessor == PpmContext.Zero)
                goto RestartModel;

            if (--orderFall == 0) {
                Successor = foundStateSuccessor;
                Allocator.Text -= (maximumContext != minimumContext) ? 1 : 0;
            }
            else if (method > ModelRestorationMethod.Freeze) {
                Successor = foundStateSuccessor;
                Allocator.Text = Allocator.Heap;
                orderFall = 0;
            }

            numberStatistics = minimumContext.NumberStatistics;
            s0 = minimumContext.SummaryFrequency - numberStatistics - foundStateFrequency;
            flag = (byte)((foundStateSymbol >= 0x40) ? 0x08 : 0x00);
            for (; currentContext != minimumContext; currentContext = currentContext.Suffix) {
                ns1 = currentContext.NumberStatistics;
                if (ns1 != 0) {
                    if ((ns1 & 1) != 0) {
                        state = Allocator.ExpandUnits(currentContext.Statistics, (ns1 + 1) >> 1);
                        if (state == PpmState.Zero)
                            goto RestartModel;
                        currentContext.Statistics = state;
                    }
                    currentContext.SummaryFrequency += (ushort)((3 * ns1 + 1 < numberStatistics) ? 1 : 0);
                }
                else {
                    state = Allocator.AllocateUnits(1);
                    if (state == PpmState.Zero)
                        goto RestartModel;
                    Copy(state, currentContext.FirstState);
                    currentContext.Statistics = state;
                    if (state.Frequency < MaximumFrequency / 4 - 1)
                        state.Frequency += state.Frequency;
                    else
                        state.Frequency = (byte)(MaximumFrequency - 4);
                    currentContext.SummaryFrequency =
                        (ushort)(state.Frequency + initialEscape + ((numberStatistics > 2) ? 1 : 0));
                }

                cf = (uint)(2 * foundStateFrequency * (currentContext.SummaryFrequency + 6));
                sf = s0 + currentContext.SummaryFrequency;

                if (cf < 6 * sf) {
                    cf = (uint)(1 + ((cf > sf) ? 1 : 0) + ((cf >= 4 * sf) ? 1 : 0));
                    currentContext.SummaryFrequency += 4;
                }
                else {
                    cf = (uint)(4 + ((cf > 9 * sf) ? 1 : 0) + ((cf > 12 * sf) ? 1 : 0) + ((cf > 15 * sf) ? 1 : 0));
                    currentContext.SummaryFrequency += (ushort)cf;
                }

                state = currentContext.Statistics + (++currentContext.NumberStatistics);
                state.Successor = Successor;
                state.Symbol = foundStateSymbol;
                state.Frequency = (byte)cf;
                currentContext.Flags |= flag;
            }

            maximumContext = foundStateSuccessor;
            return;

        RestartModel:
            RestoreModel(currentContext, minimumContext, foundStateSuccessor);
        }

        private PpmContext CreateSuccessors(bool skip, PpmState state, PpmContext context) {
            PpmContext upBranch = foundState.Successor;
            PpmState[] states = new PpmState[MaximumOrder];
            uint stateIndex = 0;
            byte symbol = foundState.Symbol;

            if (!skip) {
                states[stateIndex++] = foundState;
                if (context.Suffix == PpmContext.Zero)
                    goto NoLoop;
            }

            bool gotoLoopEntry = false;
            if (state != PpmState.Zero) {
                context = context.Suffix;
                gotoLoopEntry = true;
            }

            do {
                if (gotoLoopEntry) {
                    gotoLoopEntry = false;
                    goto LoopEntry;
                }

                context = context.Suffix;
                if (context.NumberStatistics != 0) {
                    byte temporary;
                    state = context.Statistics;
                    if (state.Symbol != symbol) {
                        do {
                            temporary = state[1].Symbol;
                            state++;
                        } while (temporary != symbol);
                    }
                    temporary = (byte)((state.Frequency < MaximumFrequency - 9) ? 1 : 0);
                    state.Frequency += temporary;
                    context.SummaryFrequency += temporary;
                }
                else {
                    state = context.FirstState;
                    state.Frequency +=
                        (byte)(((context.Suffix.NumberStatistics == 0) ? 1 : 0) & ((state.Frequency < 24) ? 1 : 0));
                }

            LoopEntry:
                if (state.Successor != upBranch) {
                    context = state.Successor;
                    break;
                }
                states[stateIndex++] = state;
            } while (context.Suffix != PpmContext.Zero);

        NoLoop:
            if (stateIndex == 0)
                return context;

            byte localNumberStatistics = 0;
            byte localFlags = (byte)((symbol >= 0x40) ? 0x10 : 0x00);
            symbol = upBranch.NumberStatistics;
            byte localSymbol = symbol;
            byte localFrequency;
            PpmContext localSuccessor = ((Pointer)upBranch) + 1;
            localFlags |= (byte)((symbol >= 0x40) ? 0x08 : 0x00);

            if (context.NumberStatistics != 0) {
                state = context.Statistics;
                if (state.Symbol != symbol) {
                    byte temporary;
                    do {
                        temporary = state[1].Symbol;
                        state++;
                    } while (temporary != symbol);
                }
                uint cf = (uint)(state.Frequency - 1);
                uint s0 = (uint)(context.SummaryFrequency - context.NumberStatistics - cf);
                localFrequency = (byte)(1 + ((2 * cf <= s0) ? (uint)((5 * cf > s0) ? 1 : 0) : ((cf + 2 * s0 - 3) / s0)));
            }
            else {
                localFrequency = context.FirstStateFrequency;
            }

            do {
                PpmContext currentContext = Allocator.AllocateContext();
                if (currentContext == PpmContext.Zero)
                    return PpmContext.Zero;
                currentContext.NumberStatistics = localNumberStatistics;
                currentContext.Flags = localFlags;
                currentContext.FirstStateSymbol = localSymbol;
                currentContext.FirstStateFrequency = localFrequency;
                currentContext.FirstStateSuccessor = localSuccessor;
                currentContext.Suffix = context;
                context = currentContext;
                states[--stateIndex].Successor = context;
            } while (stateIndex != 0);

            return context;
        }

        private PpmContext ReduceOrder(PpmState state, PpmContext context) {
            PpmState currentState;
            PpmState[] states = new PpmState[MaximumOrder];
            uint stateIndex = 0;
            PpmContext currentContext = context;
            PpmContext UpBranch = Allocator.Text;
            byte temporary;
            byte symbol = foundState.Symbol;

            states[stateIndex++] = foundState;
            foundState.Successor = UpBranch;
            orderFall++;

            bool gotoLoopEntry = false;
            if (state != PpmState.Zero) {
                context = context.Suffix;
                gotoLoopEntry = true;
            }

            while (true) {
                if (gotoLoopEntry) {
                    gotoLoopEntry = false;
                    goto LoopEntry;
                }

                if (context.Suffix == PpmContext.Zero) {
                    if (method > ModelRestorationMethod.Freeze) {
                        do {
                            states[--stateIndex].Successor = context;
                        } while (stateIndex != 0);
                        Allocator.Text = Allocator.Heap + 1;
                        orderFall = 1;
                    }
                    return context;
                }

                context = context.Suffix;
                if (context.NumberStatistics != 0) {
                    state = context.Statistics;
                    if (state.Symbol != symbol) {
                        do {
                            temporary = state[1].Symbol;
                            state++;
                        } while (temporary != symbol);
                    }
                    temporary = (byte)((state.Frequency < MaximumFrequency - 9) ? 2 : 0);
                    state.Frequency += temporary;
                    context.SummaryFrequency += temporary;
                }
                else {
                    state = context.FirstState;
                    state.Frequency += (byte)((state.Frequency < 32) ? 1 : 0);
                }

            LoopEntry:
                if (state.Successor != PpmContext.Zero)
                    break;
                states[stateIndex++] = state;
                state.Successor = UpBranch;
                orderFall++;
            }

            if (method > ModelRestorationMethod.Freeze) {
                context = state.Successor;
                do {
                    states[--stateIndex].Successor = context;
                } while (stateIndex != 0);
                Allocator.Text = Allocator.Heap + 1;
                orderFall = 1;
                return context;
            }
            else if (state.Successor <= UpBranch) {
                currentState = foundState;
                foundState = state;
                state.Successor = CreateSuccessors(false, PpmState.Zero, context);
                foundState = currentState;
            }

            if (orderFall == 1 && currentContext == maximumContext) {
                foundState.Successor = state.Successor;
                Allocator.Text--;
            }

            return state.Successor;
        }

        private void RestoreModel(PpmContext context, PpmContext minimumContext, PpmContext foundStateSuccessor) {
            PpmContext currentContext;

            Allocator.Text = Allocator.Heap;
            for (currentContext = maximumContext; currentContext != context; currentContext = currentContext.Suffix) {
                if (--currentContext.NumberStatistics == 0) {
                    currentContext.Flags =
                        (byte)
                        ((currentContext.Flags & 0x10) + ((currentContext.Statistics.Symbol >= 0x40) ? 0x08 : 0x00));
                    PpmState state = currentContext.Statistics;
                    Copy(currentContext.FirstState, state);
                    Allocator.SpecialFreeUnits(state);
                    currentContext.FirstStateFrequency = (byte)((currentContext.FirstStateFrequency + 11) >> 3);
                }
                else {
                    Refresh((uint)((currentContext.NumberStatistics + 3) >> 1), false, currentContext);
                }
            }

            for (; currentContext != minimumContext; currentContext = currentContext.Suffix) {
                if (currentContext.NumberStatistics == 0)
                    currentContext.FirstStateFrequency -= (byte)(currentContext.FirstStateFrequency >> 1);
                else if ((currentContext.SummaryFrequency += 4) > 128 + 4 * currentContext.NumberStatistics)
                    Refresh((uint)((currentContext.NumberStatistics + 2) >> 1), true, currentContext);
            }

            if (method > ModelRestorationMethod.Freeze) {
                maximumContext = foundStateSuccessor;
                Allocator.GlueCount += (uint)(((Allocator.MemoryNodes[1].Stamp & 1) == 0) ? 1 : 0);
            }
            else if (method == ModelRestorationMethod.Freeze) {
                while (maximumContext.Suffix != PpmContext.Zero)
                    maximumContext = maximumContext.Suffix;

                RemoveBinaryContexts(0, maximumContext);
                method = (ModelRestorationMethod)(method + 1);
                Allocator.GlueCount = 0;
                orderFall = modelOrder;
            }
            else if (method == ModelRestorationMethod.Restart ||
                     Allocator.GetMemoryUsed() < (Allocator.AllocatorSize >> 1)) {
                StartModel(modelOrder, method);
                escapeCount = 0;
            }
            else {
                while (maximumContext.Suffix != PpmContext.Zero)
                    maximumContext = maximumContext.Suffix;

                do {
                    CutOff(0, maximumContext);
                    Allocator.ExpandText();
                } while (Allocator.GetMemoryUsed() > 3 * (Allocator.AllocatorSize >> 2));

                Allocator.GlueCount = 0;
                orderFall = modelOrder;
            }
        }

        private static void Swap(PpmState state1, PpmState state2) {
            byte swapSymbol = state1.Symbol;
            byte swapFrequency = state1.Frequency;
            PpmContext swapSuccessor = state1.Successor;

            state1.Symbol = state2.Symbol;
            state1.Frequency = state2.Frequency;
            state1.Successor = state2.Successor;

            state2.Symbol = swapSymbol;
            state2.Frequency = swapFrequency;
            state2.Successor = swapSuccessor;
        }

        private static void Copy(PpmState state1, PpmState state2) {
            state1.Symbol = state2.Symbol;
            state1.Frequency = state2.Frequency;
            state1.Successor = state2.Successor;
        }

        private static int Mean(int sum, int shift, int round) {
            return (sum + (1 << (shift - round))) >> shift;
        }

        private void ClearMask() {
            escapeCount = 1;
            Array.Clear(characterMask, 0, characterMask.Length);
        }

        #endregion


    }
    /// <summary>
    /// The PPM context structure.  This is tightly coupled with <see cref="Model"/>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This must be a structure rather than a class because several places in the associated code assume that
    /// <see cref="PpmContext"/> is a value type (meaning that assignment creates a completely new copy of
    /// the instance rather than just copying a reference to the same instance).
    /// </para>
    /// </remarks>
    internal partial class Model {
        /// <summary>
        /// The structure which represents the current PPM context.  This is 12 bytes in size.
        /// </summary>
        internal struct PpmContext {
            public uint Address;
            public byte[] Memory;
            public static readonly PpmContext Zero = new PpmContext(0, null);
            public const int Size = 12;

            /// <summary>
            /// Initializes a new instance of the <see cref="PpmContext"/> structure.
            /// </summary>
            public PpmContext(uint address, byte[] memory) {
                Address = address;
                Memory = memory;
            }

            /// <summary>
            /// Gets or sets the number statistics.
            /// </summary>
            public byte NumberStatistics {
                get { return Memory[Address]; }
                set { Memory[Address] = value; }
            }

            /// <summary>
            /// Gets or sets the flags.
            /// </summary>
            public byte Flags {
                get { return Memory[Address + 1]; }
                set { Memory[Address + 1] = value; }
            }

            /// <summary>
            /// Gets or sets the summary frequency.
            /// </summary>
            public ushort SummaryFrequency {
                get { return (ushort)(((ushort)Memory[Address + 2]) | ((ushort)Memory[Address + 3]) << 8); }
                set {
                    Memory[Address + 2] = (byte)value;
                    Memory[Address + 3] = (byte)(value >> 8);
                }
            }

            /// <summary>
            /// Gets or sets the statistics.
            /// </summary>
            public PpmState Statistics {
                get {
                    return
                        new PpmState(
                            ((uint)Memory[Address + 4]) | ((uint)Memory[Address + 5]) << 8 |
                            ((uint)Memory[Address + 6]) << 16 | ((uint)Memory[Address + 7]) << 24, Memory);
                }
                set {
                    Memory[Address + 4] = (byte)value.Address;
                    Memory[Address + 5] = (byte)(value.Address >> 8);
                    Memory[Address + 6] = (byte)(value.Address >> 16);
                    Memory[Address + 7] = (byte)(value.Address >> 24);
                }
            }

            /// <summary>
            /// Gets or sets the suffix.
            /// </summary>
            public PpmContext Suffix {
                get {
                    return
                        new PpmContext(
                            ((uint)Memory[Address + 8]) | ((uint)Memory[Address + 9]) << 8 |
                            ((uint)Memory[Address + 10]) << 16 | ((uint)Memory[Address + 11]) << 24, Memory);
                }
                set {
                    Memory[Address + 8] = (byte)value.Address;
                    Memory[Address + 9] = (byte)(value.Address >> 8);
                    Memory[Address + 10] = (byte)(value.Address >> 16);
                    Memory[Address + 11] = (byte)(value.Address >> 24);
                }
            }

            /// <summary>
            /// The first PPM state associated with the PPM context.
            /// </summary>
            /// <remarks>
            /// <para>
            /// The first PPM state overlaps this PPM context instance (the context.SummaryFrequency and context.Statistics members
            /// of PpmContext use 6 bytes and so can therefore fit into the space used by the Symbol, Frequency and
            /// Successor members of PpmState, since they also add up to 6 bytes).
            /// </para>
            /// <para>
            /// PpmContext (context.SummaryFrequency and context.Statistics use 6 bytes)
            ///     1 context.NumberStatistics
            ///     1 context.Flags
            ///     2 context.SummaryFrequency
            ///     4 context.Statistics (pointer to PpmState)
            ///     4 context.Suffix (pointer to PpmContext)
            /// </para>
            /// <para>
            /// PpmState (total of 6 bytes)
            ///     1 Symbol
            ///     1 Frequency
            ///     4 Successor (pointer to PpmContext)
            /// </para>
            /// </remarks>
            /// <returns></returns>
            public PpmState FirstState {
                get { return new PpmState(Address + 2, Memory); }
            }

            /// <summary>
            /// Gets or sets the symbol of the first PPM state.  This is provided for convenience.  The same
            /// information can be obtained using the Symbol property on the PPM state provided by the
            /// <see cref="FirstState"/> property.
            /// </summary>
            [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Performance", "CA1811:AvoidUncalledPrivateCode",
                Justification = "The property getter is provided for completeness.")]
            public byte FirstStateSymbol {
                get { return Memory[Address + 2]; }
                set { Memory[Address + 2] = value; }
            }

            /// <summary>
            /// Gets or sets the frequency of the first PPM state.  This is provided for convenience.  The same
            /// information can be obtained using the Frequency property on the PPM state provided by the
            ///context.FirstState property.
            /// </summary>
            public byte FirstStateFrequency {
                get { return Memory[Address + 3]; }
                set { Memory[Address + 3] = value; }
            }

            /// <summary>
            /// Gets or sets the successor of the first PPM state.  This is provided for convenience.  The same
            /// information can be obtained using the Successor property on the PPM state provided by the
            /// </summary>
            [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Performance", "CA1811:AvoidUncalledPrivateCode",
                Justification = "The property getter is provided for completeness.")]
            public PpmContext FirstStateSuccessor {
                get {
                    return
                        new PpmContext(
                            ((uint)Memory[Address + 4]) | ((uint)Memory[Address + 5]) << 8 |
                            ((uint)Memory[Address + 6]) << 16 | ((uint)Memory[Address + 7]) << 24, Memory);
                }
                set {
                    Memory[Address + 4] = (byte)value.Address;
                    Memory[Address + 5] = (byte)(value.Address >> 8);
                    Memory[Address + 6] = (byte)(value.Address >> 16);
                    Memory[Address + 7] = (byte)(value.Address >> 24);
                }
            }

            /// <summary>
            /// Allow a pointer to be implicitly converted to a PPM context.
            /// </summary>
            /// <param name="pointer"></param>
            /// <returns></returns>
            public static implicit operator PpmContext(Pointer pointer) {
                return new PpmContext(pointer.Address, pointer.Memory);
            }

            /// <summary>
            /// Allow pointer-like addition on a PPM context.
            /// </summary>
            /// <param name="context"></param>
            /// <param name="offset"></param>
            /// <returns></returns>
            public static PpmContext operator +(PpmContext context, int offset) {
                context.Address = (uint)(context.Address + offset * Size);
                return context;
            }

            /// <summary>
            /// Allow pointer-like subtraction on a PPM context.
            /// </summary>
            /// <param name="context"></param>
            /// <param name="offset"></param>
            /// <returns></returns>
            public static PpmContext operator -(PpmContext context, int offset) {
                context.Address = (uint)(context.Address - offset * Size);
                return context;
            }

            /// <summary>
            /// Compare two PPM contexts.
            /// </summary>
            /// <param name="context1"></param>
            /// <param name="context2"></param>
            /// <returns></returns>
            public static bool operator <=(PpmContext context1, PpmContext context2) {
                return context1.Address <= context2.Address;
            }

            /// <summary>
            /// Compare two PPM contexts.
            /// </summary>
            /// <param name="context1"></param>
            /// <param name="context2"></param>
            /// <returns></returns>
            public static bool operator >=(PpmContext context1, PpmContext context2) {
                return context1.Address >= context2.Address;
            }

            /// <summary>
            /// Compare two PPM contexts.
            /// </summary>
            /// <param name="context1"></param>
            /// <param name="context2"></param>
            /// <returns></returns>
            public static bool operator ==(PpmContext context1, PpmContext context2) {
                return context1.Address == context2.Address;
            }

            /// <summary>
            /// Compare two PPM contexts.
            /// </summary>
            /// <param name="context1"></param>
            /// <param name="context2"></param>
            /// <returns></returns>
            public static bool operator !=(PpmContext context1, PpmContext context2) {
                return context1.Address != context2.Address;
            }

            /// <summary>
            /// Indicates whether this instance and a specified object are equal.
            /// </summary>
            /// <returns>true if obj and this instance are the same type and represent the same value; otherwise, false.</returns>
            /// <param name="obj">Another object to compare to.</param>
            public override bool Equals(object obj) {
                if (obj is PpmContext) {
                    PpmContext context = (PpmContext)obj;
                    return context.Address == Address;
                }
                return base.Equals(obj);
            }

            /// <summary>
            /// Returns the hash code for this instance.
            /// </summary>
            /// <returns>A 32-bit signed integer that is the hash code for this instance.</returns>
            public override int GetHashCode() {
                return Address.GetHashCode();
            }
        }

        private void EncodeBinarySymbol(int symbol, PpmContext context) {
            PpmState state = context.FirstState;
            int index1 = probabilities[state.Frequency - 1];
            int index2 = numberStatisticsToBinarySummaryIndex[context.Suffix.NumberStatistics] + previousSuccess +
                         context.Flags + ((runLength >> 26) & 0x20);

            if (state.Symbol == symbol) {
                foundState = state;
                state.Frequency += (byte)((state.Frequency < 196) ? 1 : 0);
                Coder.LowCount = 0;
                Coder.HighCount = binarySummary[index1, index2];
                binarySummary[index1, index2] +=
                    (ushort)(Interval - Mean(binarySummary[index1, index2], PeriodBitCount, 2));
                previousSuccess = 1;
                runLength++;
            }
            else {
                Coder.LowCount = binarySummary[index1, index2];
                binarySummary[index1, index2] -= (ushort)Mean(binarySummary[index1, index2], PeriodBitCount, 2);
                Coder.HighCount = BinaryScale;
                initialEscape = ExponentialEscapes[binarySummary[index1, index2] >> 10];
                characterMask[state.Symbol] = escapeCount;
                previousSuccess = 0;
                numberMasked = 0;
                foundState = PpmState.Zero;
            }
        }

        private void EncodeSymbol1(int symbol, PpmContext context) {
            uint lowCount;
            uint index = context.Statistics.Symbol;
            PpmState state = context.Statistics;
            Coder.Scale = context.SummaryFrequency;
            if (index == symbol) {
                Coder.HighCount = state.Frequency;
                previousSuccess = (byte)((2 * Coder.HighCount >= Coder.Scale) ? 1 : 0);
                foundState = state;
                foundState.Frequency += 4;
                context.SummaryFrequency += 4;
                runLength += previousSuccess;
                if (state.Frequency > MaximumFrequency)
                    Rescale(context);
                Coder.LowCount = 0;
                return;
            }

            lowCount = state.Frequency;
            index = context.NumberStatistics;
            previousSuccess = 0;
            while ((++state).Symbol != symbol) {
                lowCount += state.Frequency;
                if (--index == 0) {
                    Coder.LowCount = lowCount;
                    characterMask[state.Symbol] = escapeCount;
                    numberMasked = context.NumberStatistics;
                    index = context.NumberStatistics;
                    foundState = PpmState.Zero;
                    do {
                        characterMask[(--state).Symbol] = escapeCount;
                    } while (--index != 0);
                    Coder.HighCount = Coder.Scale;
                    return;
                }
            }
            Coder.HighCount = (Coder.LowCount = lowCount) + state.Frequency;
            Update1(state, context);
        }

        private void EncodeSymbol2(int symbol, PpmContext context) {
            See2Context see2Context = MakeEscapeFrequency(context);
            uint currentSymbol;
            uint lowCount = 0;
            uint index = (uint)(context.NumberStatistics - numberMasked);
            PpmState state = context.Statistics - 1;

            do {
                do {
                    currentSymbol = state[1].Symbol;
                    state++;
                } while (characterMask[currentSymbol] == escapeCount);
                characterMask[currentSymbol] = escapeCount;
                if (currentSymbol == symbol)
                    goto SymbolFound;
                lowCount += state.Frequency;
            } while (--index != 0);

            Coder.LowCount = lowCount;
            Coder.Scale += Coder.LowCount;
            Coder.HighCount = Coder.Scale;
            see2Context.Summary += (ushort)Coder.Scale;
            numberMasked = context.NumberStatistics;
            return;

        SymbolFound:
            Coder.LowCount = lowCount;
            lowCount += state.Frequency;
            Coder.HighCount = lowCount;
            for (PpmState p1 = state; --index != 0; ) {
                do {
                    currentSymbol = p1[1].Symbol;
                    p1++;
                } while (characterMask[currentSymbol] == escapeCount);
                lowCount += p1.Frequency;
            }
            Coder.Scale += lowCount;
            see2Context.Update();
            Update2(state, context);
        }

        private void DecodeBinarySymbol(PpmContext context) {
            PpmState state = context.FirstState;
            int index1 = probabilities[state.Frequency - 1];
            int index2 = numberStatisticsToBinarySummaryIndex[context.Suffix.NumberStatistics] + previousSuccess +
                         context.Flags + ((runLength >> 26) & 0x20);

            if (Coder.RangeGetCurrentShiftCount(TotalBitCount) < binarySummary[index1, index2]) {
                foundState = state;
                state.Frequency += (byte)((state.Frequency < 196) ? 1 : 0);
                Coder.LowCount = 0;
                Coder.HighCount = binarySummary[index1, index2];
                binarySummary[index1, index2] +=
                    (ushort)(Interval - Mean(binarySummary[index1, index2], PeriodBitCount, 2));
                previousSuccess = 1;
                runLength++;
            }
            else {
                Coder.LowCount = binarySummary[index1, index2];
                binarySummary[index1, index2] -= (ushort)Mean(binarySummary[index1, index2], PeriodBitCount, 2);
                Coder.HighCount = BinaryScale;
                initialEscape = ExponentialEscapes[binarySummary[index1, index2] >> 10];
                characterMask[state.Symbol] = escapeCount;
                previousSuccess = 0;
                numberMasked = 0;
                foundState = PpmState.Zero;
            }
        }

        private void DecodeSymbol1(PpmContext context) {
            uint index;
            uint count;
            uint highCount = context.Statistics.Frequency;
            PpmState state = context.Statistics;
            Coder.Scale = context.SummaryFrequency;

            count = Coder.RangeGetCurrentCount();
            if (count < highCount) {
                Coder.HighCount = highCount;
                previousSuccess = (byte)((2 * Coder.HighCount >= Coder.Scale) ? 1 : 0);
                foundState = state;
                highCount += 4;
                foundState.Frequency = (byte)highCount;
                context.SummaryFrequency += 4;
                runLength += previousSuccess;
                if (highCount > MaximumFrequency)
                    Rescale(context);
                Coder.LowCount = 0;
                return;
            }

            index = context.NumberStatistics;
            previousSuccess = 0;
            while ((highCount += (++state).Frequency) <= count) {
                if (--index == 0) {
                    Coder.LowCount = highCount;
                    characterMask[state.Symbol] = escapeCount;
                    numberMasked = context.NumberStatistics;
                    index = context.NumberStatistics;
                    foundState = PpmState.Zero;
                    do {
                        characterMask[(--state).Symbol] = escapeCount;
                    } while (--index != 0);
                    Coder.HighCount = Coder.Scale;
                    return;
                }
            }
            Coder.HighCount = highCount;
            Coder.LowCount = Coder.HighCount - state.Frequency;
            Update1(state, context);
        }

        private void DecodeSymbol2(PpmContext context) {
            See2Context see2Context = MakeEscapeFrequency(context);
            uint currentSymbol;
            uint count;
            uint highCount = 0;
            uint index = (uint)(context.NumberStatistics - numberMasked);
            uint stateIndex = 0;
            PpmState state = context.Statistics - 1;

            do {
                do {
                    currentSymbol = state[1].Symbol;
                    state++;
                } while (characterMask[currentSymbol] == escapeCount);
                highCount += state.Frequency;
                decodeStates[stateIndex++] = state;
                // note that decodeStates is a static array that is re-used on each call to this method (for performance reasons)
            } while (--index != 0);

            Coder.Scale += highCount;
            count = Coder.RangeGetCurrentCount();
            stateIndex = 0;
            state = decodeStates[stateIndex];
            if (count < highCount) {
                highCount = 0;
                while ((highCount += state.Frequency) <= count)
                    state = decodeStates[++stateIndex];
                Coder.HighCount = highCount;
                Coder.LowCount = Coder.HighCount - state.Frequency;
                see2Context.Update();
                Update2(state, context);
            }
            else {
                Coder.LowCount = highCount;
                Coder.HighCount = Coder.Scale;
                index = (uint)(context.NumberStatistics - numberMasked);
                numberMasked = context.NumberStatistics;
                do {
                    characterMask[decodeStates[stateIndex].Symbol] = escapeCount;
                    stateIndex++;
                } while (--index != 0);
                see2Context.Summary += (ushort)Coder.Scale;
            }
        }

        private void Update1(PpmState state, PpmContext context) {
            foundState = state;
            foundState.Frequency += 4;
            context.SummaryFrequency += 4;
            if (state[0].Frequency > state[-1].Frequency) {
                Swap(state[0], state[-1]);
                foundState = --state;
                if (state.Frequency > MaximumFrequency)
                    Rescale(context);
            }
        }

        private void Update2(PpmState state, PpmContext context) {
            foundState = state;
            foundState.Frequency += 4;
            context.SummaryFrequency += 4;
            if (state.Frequency > MaximumFrequency)
                Rescale(context);
            escapeCount++;
            runLength = initialRunLength;
        }

        private See2Context MakeEscapeFrequency(PpmContext context) {
            uint numberStatistics = (uint)2 * context.NumberStatistics;
            See2Context see2Context;

            if (context.NumberStatistics != 0xff) {
                // Note that context.Flags is always in the range 0 .. 28 (this ensures that the index used for the second
                // dimension of the see2Contexts array is always in the range 0 .. 31).

                numberStatistics = context.Suffix.NumberStatistics;
                int index1 = probabilities[context.NumberStatistics + 2] - 3;
                int index2 = ((context.SummaryFrequency > 11 * (context.NumberStatistics + 1)) ? 1 : 0) +
                             ((2 * context.NumberStatistics < numberStatistics + numberMasked) ? 2 : 0) + context.Flags;
                see2Context = see2Contexts[index1, index2];
                Coder.Scale = see2Context.Mean();
            }
            else {
                see2Context = emptySee2Context;
                Coder.Scale = 1;
            }

            return see2Context;
        }

        private void Rescale(PpmContext context) {
            uint oldUnitCount;
            int adder;
            uint escapeFrequency;
            uint index = context.NumberStatistics;

            byte localSymbol;
            byte localFrequency;
            PpmContext localSuccessor;
            PpmState p1;
            PpmState state;

            for (state = foundState; state != context.Statistics; state--)
                Swap(state[0], state[-1]);

            state.Frequency += 4;
            context.SummaryFrequency += 4;
            escapeFrequency = (uint)(context.SummaryFrequency - state.Frequency);
            adder = (orderFall != 0 || method > ModelRestorationMethod.Freeze) ? 1 : 0;
            state.Frequency = (byte)((state.Frequency + adder) >> 1);
            context.SummaryFrequency = state.Frequency;

            do {
                escapeFrequency -= (++state).Frequency;
                state.Frequency = (byte)((state.Frequency + adder) >> 1);
                context.SummaryFrequency += state.Frequency;
                if (state[0].Frequency > state[-1].Frequency) {
                    p1 = state;
                    localSymbol = p1.Symbol;
                    localFrequency = p1.Frequency;
                    localSuccessor = p1.Successor;
                    do {
                        Copy(p1[0], p1[-1]);
                    } while (localFrequency > (--p1)[-1].Frequency);
                    p1.Symbol = localSymbol;
                    p1.Frequency = localFrequency;
                    p1.Successor = localSuccessor;
                }
            } while (--index != 0);

            if (state.Frequency == 0) {
                do {
                    index++;
                } while ((--state).Frequency == 0);

                escapeFrequency += index;
                oldUnitCount = (uint)((context.NumberStatistics + 2) >> 1);
                context.NumberStatistics -= (byte)index;
                if (context.NumberStatistics == 0) {
                    localSymbol = context.Statistics.Symbol;
                    localFrequency = context.Statistics.Frequency;
                    localSuccessor = context.Statistics.Successor;
                    localFrequency = (byte)((2 * localFrequency + escapeFrequency - 1) / escapeFrequency);
                    if (localFrequency > MaximumFrequency / 3)
                        localFrequency = (byte)(MaximumFrequency / 3);
                    Allocator.FreeUnits(context.Statistics, oldUnitCount);
                    context.FirstStateSymbol = localSymbol;
                    context.FirstStateFrequency = localFrequency;
                    context.FirstStateSuccessor = localSuccessor;
                    context.Flags = (byte)((context.Flags & 0x10) + ((localSymbol >= 0x40) ? 0x08 : 0x00));
                    foundState = context.FirstState;
                    return;
                }

                context.Statistics = Allocator.ShrinkUnits(context.Statistics, oldUnitCount,
                                                           (uint)((context.NumberStatistics + 2) >> 1));
                context.Flags &= 0xf7;
                index = context.NumberStatistics;
                state = context.Statistics;
                context.Flags |= (byte)((state.Symbol >= 0x40) ? 0x08 : 0x00);
                do {
                    context.Flags |= (byte)(((++state).Symbol >= 0x40) ? 0x08 : 0x00);
                } while (--index != 0);
            }

            escapeFrequency -= (escapeFrequency >> 1);
            context.SummaryFrequency += (ushort)escapeFrequency;
            context.Flags |= 0x04;
            foundState = context.Statistics;
        }

        private void Refresh(uint oldUnitCount, bool scale, PpmContext context) {
            int index = context.NumberStatistics;
            int escapeFrequency;
            int scaleValue = (scale ? 1 : 0);

            context.Statistics = Allocator.ShrinkUnits(context.Statistics, oldUnitCount, (uint)((index + 2) >> 1));
            PpmState statistics = context.Statistics;
            context.Flags =
                (byte)((context.Flags & (0x10 + (scale ? 0x04 : 0x00))) + ((statistics.Symbol >= 0x40) ? 0x08 : 0x00));
            escapeFrequency = context.SummaryFrequency - statistics.Frequency;
            statistics.Frequency = (byte)((statistics.Frequency + scaleValue) >> scaleValue);
            context.SummaryFrequency = statistics.Frequency;

            do {
                escapeFrequency -= (++statistics).Frequency;
                statistics.Frequency = (byte)((statistics.Frequency + scaleValue) >> scaleValue);
                context.SummaryFrequency += statistics.Frequency;
                context.Flags |= (byte)((statistics.Symbol >= 0x40) ? 0x08 : 0x00);
            } while (--index != 0);

            escapeFrequency = (escapeFrequency + scaleValue) >> scaleValue;
            context.SummaryFrequency += (ushort)escapeFrequency;
        }

        private PpmContext CutOff(int order, PpmContext context) {
            int index;
            PpmState state;

            if (context.NumberStatistics == 0) {
                state = context.FirstState;
                if ((Pointer)state.Successor >= Allocator.BaseUnit) {
                    if (order < modelOrder)
                        state.Successor = CutOff(order + 1, state.Successor);
                    else
                        state.Successor = PpmContext.Zero;

                    if (state.Successor == PpmContext.Zero && order > OrderBound) {
                        Allocator.SpecialFreeUnits(context);
                        return PpmContext.Zero;
                    }

                    return context;
                }
                else {
                    Allocator.SpecialFreeUnits(context);
                    return PpmContext.Zero;
                }
            }

            uint unitCount = (uint)((context.NumberStatistics + 2) >> 1);
            context.Statistics = Allocator.MoveUnitsUp(context.Statistics, unitCount);
            index = context.NumberStatistics;
            for (state = context.Statistics + index; state >= context.Statistics; state--) {
                if (state.Successor < Allocator.BaseUnit) {
                    state.Successor = PpmContext.Zero;
                    Swap(state, context.Statistics[index--]);
                }
                else if (order < modelOrder)
                    state.Successor = CutOff(order + 1, state.Successor);
                else
                    state.Successor = PpmContext.Zero;
            }

            if (index != context.NumberStatistics && order != 0) {
                context.NumberStatistics = (byte)index;
                state = context.Statistics;
                if (index < 0) {
                    Allocator.FreeUnits(state, unitCount);
                    Allocator.SpecialFreeUnits(context);
                    return PpmContext.Zero;
                }
                else if (index == 0) {
                    context.Flags = (byte)((context.Flags & 0x10) + ((state.Symbol >= 0x40) ? 0x08 : 0x00));
                    Copy(context.FirstState, state);
                    Allocator.FreeUnits(state, unitCount);
                    context.FirstStateFrequency = (byte)((context.FirstStateFrequency + 11) >> 3);
                }
                else {
                    Refresh(unitCount, context.SummaryFrequency > 16 * index, context);
                }
            }

            return context;
        }

        private PpmContext RemoveBinaryContexts(int order, PpmContext context) {
            if (context.NumberStatistics == 0) {
                PpmState state = context.FirstState;
                if ((Pointer)state.Successor >= Allocator.BaseUnit && order < modelOrder)
                    state.Successor = RemoveBinaryContexts(order + 1, state.Successor);
                else
                    state.Successor = PpmContext.Zero;
                if ((state.Successor == PpmContext.Zero) &&
                    (context.Suffix.NumberStatistics == 0 || context.Suffix.Flags == 0xff)) {
                    Allocator.FreeUnits(context, 1);
                    return PpmContext.Zero;
                }
                else {
                    return context;
                }
            }

            for (PpmState state = context.Statistics + context.NumberStatistics; state >= context.Statistics; state--) {
                if ((Pointer)state.Successor >= Allocator.BaseUnit && order < modelOrder)
                    state.Successor = RemoveBinaryContexts(order + 1, state.Successor);
                else
                    state.Successor = PpmContext.Zero;
            }

            return context;
        }
    }
}

