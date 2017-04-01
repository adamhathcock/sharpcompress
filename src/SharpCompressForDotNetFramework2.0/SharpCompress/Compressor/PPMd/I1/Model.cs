#region Using

using System;
using System.IO;

#endregion

// This is a port of Dmitry Shkarin's PPMd Variant I Revision 1.
// Ported by Michael Bone (mjbone03@yahoo.com.au).
namespace SharpCompress.Compressor.PPMd.I1
{
    /// <summary>
    /// The model.
    /// </summary>
    internal partial class Model
    {
        public const uint Signature = 0x84acaf8fU;
        public const char Variant = 'I';
        public const int MaximumOrder = 16;  // maximum allowed model order

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
        private ushort[,] binarySummary = new ushort[25, 64];  // binary SEE-contexts
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
        private PpmState foundState;  // found next state transition

        private Allocator Allocator;
        private Coder Coder;
        private PpmContext minimumContext;
        private byte numberStatistics;
        private PpmState[] decodeStates = new PpmState[256];

        private static readonly ushort[] InitialBinaryEscapes = { 0x3CDD, 0x1F3F, 0x59BF, 0x48F3, 0x64A1, 0x5ABC, 0x6632, 0x6051 };
        private static readonly byte[] ExponentialEscapes = { 25, 14, 9, 7, 5, 5, 4, 4, 4, 3, 3, 3, 2, 2, 2, 2 };

        #region Public Methods

        public Model()
        {
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

            for (int index = UpperFrequency; index < 260; index++)
            {
                probabilities[index] = (byte)probability;
                count--;
                if (count == 0)
                {
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
        public void Encode(Stream target, Stream source, PpmdProperties properties)
        {
            if (target == null)
                throw new ArgumentNullException("target");

            if (source == null)
                throw new ArgumentNullException("source");

            EncodeStart(properties);
            EncodeBlock(target, source, true);
        }

        internal Coder EncodeStart(PpmdProperties properties)
        {
            Allocator = properties.Allocator;
            Coder = new Coder();
            Coder.RangeEncoderInitialize();
            StartModel(properties.ModelOrder, properties.ModelRestorationMethod);
            return Coder;
        }

        internal void EncodeBlock(Stream target, Stream source, bool final)
        {
            while (true)
            {
                minimumContext = maximumContext;
                numberStatistics = minimumContext.NumberStatistics;

                int c = source.ReadByte();
                if (c < 0 && !final)
                    return;

                if (numberStatistics != 0)
                {
                    EncodeSymbol1(c, minimumContext);
                    Coder.RangeEncodeSymbol();
                }
                else
                {
                    EncodeBinarySymbol(c, minimumContext);
                    Coder.RangeShiftEncodeSymbol(TotalBitCount);
                }

                while (foundState == PpmState.Zero)
                {
                    Coder.RangeEncoderNormalize(target);
                    do
                    {
                        orderFall++;
                        minimumContext = minimumContext.Suffix;
                        if (minimumContext == PpmContext.Zero)
                            goto StopEncoding;
                    } while (minimumContext.NumberStatistics == numberMasked);
                    EncodeSymbol2(c, minimumContext);
                    Coder.RangeEncodeSymbol();
                }

                if (orderFall == 0 && (Pointer)foundState.Successor >= Allocator.BaseUnit)
                {
                    maximumContext = foundState.Successor;
                }
                else
                {
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
        public void Decode(Stream target, Stream source, PpmdProperties properties)
        {
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

        internal Coder DecodeStart(Stream source, PpmdProperties properties)
        {
            Allocator = properties.Allocator;
            Coder = new Coder();
            Coder.RangeDecoderInitialize(source);
            StartModel(properties.ModelOrder, properties.ModelRestorationMethod);
            minimumContext = maximumContext;
            numberStatistics = minimumContext.NumberStatistics;
            return Coder;
        }

        internal int DecodeBlock(Stream source, byte[] buffer, int offset, int count)
        {
            if (minimumContext == PpmContext.Zero)
                return 0;

            int total = 0;
            while (total < count)
            {
                if (numberStatistics != 0)
                    DecodeSymbol1(minimumContext);
                else
                    DecodeBinarySymbol(minimumContext);

                Coder.RangeRemoveSubrange();

                while (foundState == PpmState.Zero)
                {
                    Coder.RangeDecoderNormalize(source);
                    do
                    {
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

                if (orderFall == 0 && (Pointer)foundState.Successor >= Allocator.BaseUnit)
                {
                    maximumContext = foundState.Successor;
                }
                else
                {
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
        private void StartModel(int modelOrder, ModelRestorationMethod modelRestorationMethod)
        {
            Array.Clear(characterMask, 0, characterMask.Length);
            escapeCount = 1;

            // Compress in "solid" mode if the model order value is set to 1 (this will examine the current PPM context
            // structures to determine the value of orderFall).

            if (modelOrder < 2)
            {
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
            maximumContext.Statistics = Allocator.AllocateUnits(256 / 2);  // allocates enough space for 256 PPM states (each is 6 bytes)

            previousSuccess = 0;
            for (int index = 0; index < 256; index++)
            {
                PpmState state = maximumContext.Statistics[index];
                state.Symbol = (byte)index;
                state.Frequency = 1;
                state.Successor = PpmContext.Zero;
            }

            uint probability = 0;
            for (int index1 = 0; probability < 25; probability++)
            {
                while (probabilities[index1] == probability)
                    index1++;
                for (int index2 = 0; index2 < 8; index2++)
                    binarySummary[probability, index2] = (ushort)(BinaryScale - InitialBinaryEscapes[index2] / (index1 + 1));
                for (int index2 = 8; index2 < 64; index2 += 8)
                    for (int index3 = 0; index3 < 8; index3++)
                        binarySummary[probability, index2 + index3] = binarySummary[probability, index3];
            }

            probability = 0;
            for (uint index1 = 0; probability < 24; probability++)
            {
                while (probabilities[index1 + 3] == probability + 3)
                    index1++;
                for (int index2 = 0; index2 < 32; index2++)
                    see2Contexts[probability, index2].Initialize(2 * index1 + 5);
            }
        }

        private void UpdateModel(PpmContext minimumContext)
        {
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

            if ((foundStateFrequency < MaximumFrequency / 4) && (context != PpmContext.Zero))
            {
                if (context.NumberStatistics != 0)
                {
                    state = context.Statistics;
                    if (state.Symbol != foundStateSymbol)
                    {
                        do
                        {
                            symbol = state[1].Symbol;
                            state++;
                        } while (symbol != foundStateSymbol);
                        if (state[0].Frequency >= state[-1].Frequency)
                        {
                            Swap(state[0], state[-1]);
                            state--;
                        }
                    }
                    cf = (uint)((state.Frequency < MaximumFrequency - 9) ? 2 : 0);
                    state.Frequency += (byte)cf;
                    context.SummaryFrequency += (byte)cf;
                }
                else
                {
                    state = context.FirstState;
                    state.Frequency += (byte)((state.Frequency < 32) ? 1 : 0);
                }
            }

            if (orderFall == 0 && foundStateSuccessor != PpmContext.Zero)
            {
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

            if (foundStateSuccessor != PpmContext.Zero)
            {
                if (foundStateSuccessor < Allocator.BaseUnit)
                    foundStateSuccessor = CreateSuccessors(false, state, minimumContext);
            }
            else
            {
                foundStateSuccessor = ReduceOrder(state, minimumContext);
            }

            if (foundStateSuccessor == PpmContext.Zero)
                goto RestartModel;

            if (--orderFall == 0)
            {
                Successor = foundStateSuccessor;
                Allocator.Text -= (maximumContext != minimumContext) ? 1 : 0;
            }
            else if (method > ModelRestorationMethod.Freeze)
            {
                Successor = foundStateSuccessor;
                Allocator.Text = Allocator.Heap;
                orderFall = 0;
            }

            numberStatistics = minimumContext.NumberStatistics;
            s0 = minimumContext.SummaryFrequency - numberStatistics - foundStateFrequency;
            flag = (byte)((foundStateSymbol >= 0x40) ? 0x08 : 0x00);
            for (; currentContext != minimumContext; currentContext = currentContext.Suffix)
            {
                ns1 = currentContext.NumberStatistics;
                if (ns1 != 0)
                {
                    if ((ns1 & 1) != 0)
                    {
                        state = Allocator.ExpandUnits(currentContext.Statistics, (ns1 + 1) >> 1);
                        if (state == PpmState.Zero)
                            goto RestartModel;
                        currentContext.Statistics = state;
                    }
                    currentContext.SummaryFrequency += (ushort)((3 * ns1 + 1 < numberStatistics) ? 1 : 0);
                }
                else
                {
                    state = Allocator.AllocateUnits(1);
                    if (state == PpmState.Zero)
                        goto RestartModel;
                    Copy(state, currentContext.FirstState);
                    currentContext.Statistics = state;
                    if (state.Frequency < MaximumFrequency / 4 - 1)
                        state.Frequency += state.Frequency;
                    else
                        state.Frequency = (byte)(MaximumFrequency - 4);
                    currentContext.SummaryFrequency = (ushort)(state.Frequency + initialEscape + ((numberStatistics > 2) ? 1 : 0));
                }

                cf = (uint)(2 * foundStateFrequency * (currentContext.SummaryFrequency + 6));
                sf = s0 + currentContext.SummaryFrequency;

                if (cf < 6 * sf)
                {
                    cf = (uint)(1 + ((cf > sf) ? 1 : 0) + ((cf >= 4 * sf) ? 1 : 0));
                    currentContext.SummaryFrequency += 4;
                }
                else
                {
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

        private PpmContext CreateSuccessors(bool skip, PpmState state, PpmContext context)
        {
            PpmContext upBranch = foundState.Successor;
            PpmState[] states = new PpmState[MaximumOrder];
            uint stateIndex = 0;
            byte symbol = foundState.Symbol;

            if (!skip)
            {
                states[stateIndex++] = foundState;
                if (context.Suffix == PpmContext.Zero)
                    goto NoLoop;
            }

            bool gotoLoopEntry = false;
            if (state != PpmState.Zero)
            {
                context = context.Suffix;
                gotoLoopEntry = true;
            }

            do
            {
                if (gotoLoopEntry)
                {
                    gotoLoopEntry = false;
                    goto LoopEntry;
                }

                context = context.Suffix;
                if (context.NumberStatistics != 0)
                {
                    byte temporary;
                    state = context.Statistics;
                    if (state.Symbol != symbol)
                    {
                        do
                        {
                            temporary = state[1].Symbol;
                            state++;
                        } while (temporary != symbol);
                    }
                    temporary = (byte)((state.Frequency < MaximumFrequency - 9) ? 1 : 0);
                    state.Frequency += temporary;
                    context.SummaryFrequency += temporary;
                }
                else
                {
                    state = context.FirstState;
                    state.Frequency += (byte)(((context.Suffix.NumberStatistics == 0) ? 1 : 0) & ((state.Frequency < 24) ? 1 : 0));
                }

            LoopEntry:
                if (state.Successor != upBranch)
                {
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

            if (context.NumberStatistics != 0)
            {
                state = context.Statistics;
                if (state.Symbol != symbol)
                {
                    byte temporary;
                    do
                    {
                        temporary = state[1].Symbol;
                        state++;
                    } while (temporary != symbol);
                }
                uint cf = (uint)(state.Frequency - 1);
                uint s0 = (uint)(context.SummaryFrequency - context.NumberStatistics - cf);
                localFrequency = (byte)(1 + ((2 * cf <= s0) ? (uint)((5 * cf > s0) ? 1 : 0) : ((cf + 2 * s0 - 3) / s0)));
            }
            else
            {
                localFrequency = context.FirstStateFrequency;
            }

            do
            {
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

        private PpmContext ReduceOrder(PpmState state, PpmContext context)
        {
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
            if (state != PpmState.Zero)
            {
                context = context.Suffix;
                gotoLoopEntry = true;
            }

            while (true)
            {
                if (gotoLoopEntry)
                {
                    gotoLoopEntry = false;
                    goto LoopEntry;
                }

                if (context.Suffix == PpmContext.Zero)
                {
                    if (method > ModelRestorationMethod.Freeze)
                    {
                        do
                        {
                            states[--stateIndex].Successor = context;
                        } while (stateIndex != 0);
                        Allocator.Text = Allocator.Heap + 1;
                        orderFall = 1;
                    }
                    return context;
                }

                context = context.Suffix;
                if (context.NumberStatistics != 0)
                {
                    state = context.Statistics;
                    if (state.Symbol != symbol)
                    {
                        do
                        {
                            temporary = state[1].Symbol;
                            state++;
                        } while (temporary != symbol);
                    }
                    temporary = (byte)((state.Frequency < MaximumFrequency - 9) ? 2 : 0);
                    state.Frequency += temporary;
                    context.SummaryFrequency += temporary;
                }
                else
                {
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

            if (method > ModelRestorationMethod.Freeze)
            {
                context = state.Successor;
                do
                {
                    states[--stateIndex].Successor = context;
                } while (stateIndex != 0);
                Allocator.Text = Allocator.Heap + 1;
                orderFall = 1;
                return context;
            }
            else if (state.Successor <= UpBranch)
            {
                currentState = foundState;
                foundState = state;
                state.Successor = CreateSuccessors(false, PpmState.Zero, context);
                foundState = currentState;
            }

            if (orderFall == 1 && currentContext == maximumContext)
            {
                foundState.Successor = state.Successor;
                Allocator.Text--;
            }

            return state.Successor;
        }

        private void RestoreModel(PpmContext context, PpmContext minimumContext, PpmContext foundStateSuccessor)
        {
            PpmContext currentContext;

            Allocator.Text = Allocator.Heap;
            for (currentContext = maximumContext; currentContext != context; currentContext = currentContext.Suffix)
            {
                if (--currentContext.NumberStatistics == 0)
                {
                    currentContext.Flags = (byte)((currentContext.Flags & 0x10) + ((currentContext.Statistics.Symbol >= 0x40) ? 0x08 : 0x00));
                    PpmState state = currentContext.Statistics;
                    Copy(currentContext.FirstState, state);
                    Allocator.SpecialFreeUnits(state);
                    currentContext.FirstStateFrequency = (byte)((currentContext.FirstStateFrequency + 11) >> 3);
                }
                else
                {
                    Refresh((uint)((currentContext.NumberStatistics + 3) >> 1), false, currentContext);
                }
            }

            for (; currentContext != minimumContext; currentContext = currentContext.Suffix)
            {
                if (currentContext.NumberStatistics == 0)
                    currentContext.FirstStateFrequency -= (byte)(currentContext.FirstStateFrequency >> 1);
                else if ((currentContext.SummaryFrequency += 4) > 128 + 4 * currentContext.NumberStatistics)
                    Refresh((uint)((currentContext.NumberStatistics + 2) >> 1), true, currentContext);
            }

            if (method > ModelRestorationMethod.Freeze)
            {
                maximumContext = foundStateSuccessor;
                Allocator.GlueCount += (uint)(((Allocator.MemoryNodes[1].Stamp & 1) == 0) ? 1 : 0);
            }
            else if (method == ModelRestorationMethod.Freeze)
            {
                while (maximumContext.Suffix != PpmContext.Zero)
                    maximumContext = maximumContext.Suffix;

                RemoveBinaryContexts(0, maximumContext);
                method = (ModelRestorationMethod)(method + 1);
                Allocator.GlueCount = 0;
                orderFall = modelOrder;
            }
            else if (method == ModelRestorationMethod.Restart || Allocator.GetMemoryUsed() < (Allocator.AllocatorSize >> 1))
            {
                StartModel(modelOrder, method);
                escapeCount = 0;
            }
            else
            {
                while (maximumContext.Suffix != PpmContext.Zero)
                    maximumContext = maximumContext.Suffix;

                do
                {
                    CutOff(0, maximumContext);
                    Allocator.ExpandText();
                } while (Allocator.GetMemoryUsed() > 3 * (Allocator.AllocatorSize >> 2));

                Allocator.GlueCount = 0;
                orderFall = modelOrder;
            }
        }

        private static void Swap(PpmState state1, PpmState state2)
        {
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

        private static void Copy(PpmState state1, PpmState state2)
        {
            state1.Symbol = state2.Symbol;
            state1.Frequency = state2.Frequency;
            state1.Successor = state2.Successor;
        }

        private static int Mean(int sum, int shift, int round)
        {
            return (sum + (1 << (shift - round))) >> shift;
        }

        private void ClearMask()
        {
            escapeCount = 1;
            Array.Clear(characterMask, 0, characterMask.Length);
        }

        #endregion
    }
}
