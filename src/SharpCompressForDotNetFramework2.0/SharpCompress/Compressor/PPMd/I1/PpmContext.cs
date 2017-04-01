#region Using



#endregion

namespace SharpCompress.Compressor.PPMd.I1
{
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
    internal partial class Model
    {
        /// <summary>
        /// The structure which represents the current PPM context.  This is 12 bytes in size.
        /// </summary>
        internal struct PpmContext
        {
            public uint Address;
            public byte[] Memory;
            public static readonly PpmContext Zero = new PpmContext(0, null);
            public const int Size = 12;

            /// <summary>
            /// Initializes a new instance of the <see cref="PpmContext"/> structure.
            /// </summary>
            public PpmContext(uint address, byte[] memory)
            {
                Address = address;
                Memory = memory;
            }

            /// <summary>
            /// Gets or sets the number statistics.
            /// </summary>
            public byte NumberStatistics
            {
                get { return Memory[Address]; }
                set { Memory[Address] = value; }
            }

            /// <summary>
            /// Gets or sets the flags.
            /// </summary>
            public byte Flags
            {
                get { return Memory[Address + 1]; }
                set { Memory[Address + 1] = value; }
            }

            /// <summary>
            /// Gets or sets the summary frequency.
            /// </summary>
            public ushort SummaryFrequency
            {
                get { return (ushort)(((ushort)Memory[Address + 2]) | ((ushort)Memory[Address + 3]) << 8); }
                set
                {
                    Memory[Address + 2] = (byte)value;
                    Memory[Address + 3] = (byte)(value >> 8);
                }
            }

            /// <summary>
            /// Gets or sets the statistics.
            /// </summary>
            public PpmState Statistics
            {
                get { return new PpmState(((uint)Memory[Address + 4]) | ((uint)Memory[Address + 5]) << 8 | ((uint)Memory[Address + 6]) << 16 | ((uint)Memory[Address + 7]) << 24, Memory); }
                set
                {
                    Memory[Address + 4] = (byte)value.Address;
                    Memory[Address + 5] = (byte)(value.Address >> 8);
                    Memory[Address + 6] = (byte)(value.Address >> 16);
                    Memory[Address + 7] = (byte)(value.Address >> 24);
                }
            }

            /// <summary>
            /// Gets or sets the suffix.
            /// </summary>
            public PpmContext Suffix
            {
                get { return new PpmContext(((uint)Memory[Address + 8]) | ((uint)Memory[Address + 9]) << 8 | ((uint)Memory[Address + 10]) << 16 | ((uint)Memory[Address + 11]) << 24, Memory); }
                set
                {
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
            public PpmState FirstState
            {
                get { return new PpmState(Address + 2, Memory); }
            }

            /// <summary>
            /// Gets or sets the symbol of the first PPM state.  This is provided for convenience.  The same
            /// information can be obtained using the Symbol property on the PPM state provided by the
            /// <see cref="FirstState"/> property.
            /// </summary>
            [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Performance", "CA1811:AvoidUncalledPrivateCode", Justification = "The property getter is provided for completeness.")]
            public byte FirstStateSymbol
            {
                get { return Memory[Address + 2]; }
                set { Memory[Address + 2] = value; }
            }

            /// <summary>
            /// Gets or sets the frequency of the first PPM state.  This is provided for convenience.  The same
            /// information can be obtained using the Frequency property on the PPM state provided by the
            ///context.FirstState property.
            /// </summary>
            public byte FirstStateFrequency
            {
                get { return Memory[Address + 3]; }
                set { Memory[Address + 3] = value; }
            }

            /// <summary>
            /// Gets or sets the successor of the first PPM state.  This is provided for convenience.  The same
            /// information can be obtained using the Successor property on the PPM state provided by the
            /// </summary>
            [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Performance", "CA1811:AvoidUncalledPrivateCode", Justification = "The property getter is provided for completeness.")]
            public PpmContext FirstStateSuccessor
            {
                get { return new PpmContext(((uint)Memory[Address + 4]) | ((uint)Memory[Address + 5]) << 8 | ((uint)Memory[Address + 6]) << 16 | ((uint)Memory[Address + 7]) << 24, Memory); }
                set
                {
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
            public static implicit operator PpmContext(Pointer pointer)
            {
                return new PpmContext(pointer.Address, pointer.Memory);
            }

            /// <summary>
            /// Allow pointer-like addition on a PPM context.
            /// </summary>
            /// <param name="context"></param>
            /// <param name="offset"></param>
            /// <returns></returns>
            public static PpmContext operator +(PpmContext context, int offset)
            {
                context.Address = (uint)(context.Address + offset * Size);
                return context;
            }

            /// <summary>
            /// Allow pointer-like subtraction on a PPM context.
            /// </summary>
            /// <param name="context"></param>
            /// <param name="offset"></param>
            /// <returns></returns>
            public static PpmContext operator -(PpmContext context, int offset)
            {
                context.Address = (uint)(context.Address - offset * Size);
                return context;
            }

            /// <summary>
            /// Compare two PPM contexts.
            /// </summary>
            /// <param name="context1"></param>
            /// <param name="context2"></param>
            /// <returns></returns>
            public static bool operator <=(PpmContext context1, PpmContext context2)
            {
                return context1.Address <= context2.Address;
            }

            /// <summary>
            /// Compare two PPM contexts.
            /// </summary>
            /// <param name="context1"></param>
            /// <param name="context2"></param>
            /// <returns></returns>
            public static bool operator >=(PpmContext context1, PpmContext context2)
            {
                return context1.Address >= context2.Address;
            }

            /// <summary>
            /// Compare two PPM contexts.
            /// </summary>
            /// <param name="context1"></param>
            /// <param name="context2"></param>
            /// <returns></returns>
            public static bool operator ==(PpmContext context1, PpmContext context2)
            {
                return context1.Address == context2.Address;
            }

            /// <summary>
            /// Compare two PPM contexts.
            /// </summary>
            /// <param name="context1"></param>
            /// <param name="context2"></param>
            /// <returns></returns>
            public static bool operator !=(PpmContext context1, PpmContext context2)
            {
                return context1.Address != context2.Address;
            }

            /// <summary>
            /// Indicates whether this instance and a specified object are equal.
            /// </summary>
            /// <returns>true if obj and this instance are the same type and represent the same value; otherwise, false.</returns>
            /// <param name="obj">Another object to compare to.</param>
            public override bool Equals(object obj)
            {
                if (obj is PpmContext)
                {
                    PpmContext context = (PpmContext)obj;
                    return context.Address == Address;
                }
                return base.Equals(obj);
            }

            /// <summary>
            /// Returns the hash code for this instance.
            /// </summary>
            /// <returns>A 32-bit signed integer that is the hash code for this instance.</returns>
            public override int GetHashCode()
            {
                return Address.GetHashCode();
            }
        }

        private void EncodeBinarySymbol(int symbol, PpmContext context)
        {
            PpmState state = context.FirstState;
            int index1 = probabilities[state.Frequency - 1];
            int index2 = numberStatisticsToBinarySummaryIndex[context.Suffix.NumberStatistics] + previousSuccess + context.Flags + ((runLength >> 26) & 0x20);

            if (state.Symbol == symbol)
            {
                foundState = state;
                state.Frequency += (byte)((state.Frequency < 196) ? 1 : 0);
                Coder.LowCount = 0;
                Coder.HighCount = binarySummary[index1, index2];
                binarySummary[index1, index2] += (ushort)(Interval - Mean(binarySummary[index1, index2], PeriodBitCount, 2));
                previousSuccess = 1;
                runLength++;
            }
            else
            {
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

        private void EncodeSymbol1(int symbol, PpmContext context)
        {
            uint lowCount;
            uint index = context.Statistics.Symbol;
            PpmState state = context.Statistics;
            Coder.Scale = context.SummaryFrequency;
            if (index == symbol)
            {
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
            while ((++state).Symbol != symbol)
            {
                lowCount += state.Frequency;
                if (--index == 0)
                {
                    Coder.LowCount = lowCount;
                    characterMask[state.Symbol] = escapeCount;
                    numberMasked = context.NumberStatistics;
                    index = context.NumberStatistics;
                    foundState = PpmState.Zero;
                    do
                    {
                        characterMask[(--state).Symbol] = escapeCount;
                    } while (--index != 0);
                    Coder.HighCount = Coder.Scale;
                    return;
                }
            }
            Coder.HighCount = (Coder.LowCount = lowCount) + state.Frequency;
            Update1(state, context);
        }

        private void EncodeSymbol2(int symbol, PpmContext context)
        {
            See2Context see2Context = MakeEscapeFrequency(context);
            uint currentSymbol;
            uint lowCount = 0;
            uint index = (uint)(context.NumberStatistics - numberMasked);
            PpmState state = context.Statistics - 1;

            do
            {
                do
                {
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
            for (PpmState p1 = state; --index != 0; )
            {
                do
                {
                    currentSymbol = p1[1].Symbol;
                    p1++;
                } while (characterMask[currentSymbol] == escapeCount);
                lowCount += p1.Frequency;
            }
            Coder.Scale += lowCount;
            see2Context.Update();
            Update2(state, context);
        }

        private void DecodeBinarySymbol(PpmContext context)
        {
            PpmState state = context.FirstState;
            int index1 = probabilities[state.Frequency - 1];
            int index2 = numberStatisticsToBinarySummaryIndex[context.Suffix.NumberStatistics] + previousSuccess + context.Flags + ((runLength >> 26) & 0x20);

            if (Coder.RangeGetCurrentShiftCount(TotalBitCount) < binarySummary[index1, index2])
            {
                foundState = state;
                state.Frequency += (byte)((state.Frequency < 196) ? 1 : 0);
                Coder.LowCount = 0;
                Coder.HighCount = binarySummary[index1, index2];
                binarySummary[index1, index2] += (ushort)(Interval - Mean(binarySummary[index1, index2], PeriodBitCount, 2));
                previousSuccess = 1;
                runLength++;
            }
            else
            {
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

        private void DecodeSymbol1(PpmContext context)
        {
            uint index;
            uint count;
            uint highCount = context.Statistics.Frequency;
            PpmState state = context.Statistics;
            Coder.Scale = context.SummaryFrequency;

            count = Coder.RangeGetCurrentCount();
            if (count < highCount)
            {
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
            while ((highCount += (++state).Frequency) <= count)
            {
                if (--index == 0)
                {
                    Coder.LowCount = highCount;
                    characterMask[state.Symbol] = escapeCount;
                    numberMasked = context.NumberStatistics;
                    index = context.NumberStatistics;
                    foundState = PpmState.Zero;
                    do
                    {
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

        private void DecodeSymbol2(PpmContext context)
        {
            See2Context see2Context = MakeEscapeFrequency(context);
            uint currentSymbol;
            uint count;
            uint highCount = 0;
            uint index = (uint)(context.NumberStatistics - numberMasked);
            uint stateIndex = 0;
            PpmState state = context.Statistics - 1;

            do
            {
                do
                {
                    currentSymbol = state[1].Symbol;
                    state++;
                } while (characterMask[currentSymbol] == escapeCount);
                highCount += state.Frequency;
                decodeStates[stateIndex++] = state;  // note that decodeStates is a static array that is re-used on each call to this method (for performance reasons)
            } while (--index != 0);

            Coder.Scale += highCount;
            count = Coder.RangeGetCurrentCount();
            stateIndex = 0;
            state = decodeStates[stateIndex];
            if (count < highCount)
            {
                highCount = 0;
                while ((highCount += state.Frequency) <= count)
                    state = decodeStates[++stateIndex];
                Coder.HighCount = highCount;
                Coder.LowCount = Coder.HighCount - state.Frequency;
                see2Context.Update();
                Update2(state, context);
            }
            else
            {
                Coder.LowCount = highCount;
                Coder.HighCount = Coder.Scale;
                index = (uint)(context.NumberStatistics - numberMasked);
                numberMasked = context.NumberStatistics;
                do
                {
                    characterMask[decodeStates[stateIndex].Symbol] = escapeCount;
                    stateIndex++;
                } while (--index != 0);
                see2Context.Summary += (ushort)Coder.Scale;
            }
        }

        private void Update1(PpmState state, PpmContext context)
        {
            foundState = state;
            foundState.Frequency += 4;
            context.SummaryFrequency += 4;
            if (state[0].Frequency > state[-1].Frequency)
            {
                Swap(state[0], state[-1]);
                foundState = --state;
                if (state.Frequency > MaximumFrequency)
                    Rescale(context);
            }
        }

        private void Update2(PpmState state, PpmContext context)
        {
            foundState = state;
            foundState.Frequency += 4;
            context.SummaryFrequency += 4;
            if (state.Frequency > MaximumFrequency)
                Rescale(context);
            escapeCount++;
            runLength = initialRunLength;
        }

        private See2Context MakeEscapeFrequency(PpmContext context)
        {
            uint numberStatistics = (uint)2 * context.NumberStatistics;
            See2Context see2Context;

            if (context.NumberStatistics != 0xff)
            {
                // Note that context.Flags is always in the range 0 .. 28 (this ensures that the index used for the second
                // dimension of the see2Contexts array is always in the range 0 .. 31).

                numberStatistics = context.Suffix.NumberStatistics;
                int index1 = probabilities[context.NumberStatistics + 2] - 3;
                int index2 = ((context.SummaryFrequency > 11 * (context.NumberStatistics + 1)) ? 1 : 0) + ((2 * context.NumberStatistics < numberStatistics + numberMasked) ? 2 : 0) + context.Flags;
                see2Context = see2Contexts[index1, index2];
                Coder.Scale = see2Context.Mean();
            }
            else
            {
                see2Context = emptySee2Context;
                Coder.Scale = 1;
            }

            return see2Context;
        }

        private void Rescale(PpmContext context)
        {
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

            do
            {
                escapeFrequency -= (++state).Frequency;
                state.Frequency = (byte)((state.Frequency + adder) >> 1);
                context.SummaryFrequency += state.Frequency;
                if (state[0].Frequency > state[-1].Frequency)
                {
                    p1 = state;
                    localSymbol = p1.Symbol;
                    localFrequency = p1.Frequency;
                    localSuccessor = p1.Successor;
                    do
                    {
                        Copy(p1[0], p1[-1]);
                    } while (localFrequency > (--p1)[-1].Frequency);
                    p1.Symbol = localSymbol;
                    p1.Frequency = localFrequency;
                    p1.Successor = localSuccessor;
                }
            } while (--index != 0);

            if (state.Frequency == 0)
            {
                do
                {
                    index++;
                } while ((--state).Frequency == 0);

                escapeFrequency += index;
                oldUnitCount = (uint)((context.NumberStatistics + 2) >> 1);
                context.NumberStatistics -= (byte)index;
                if (context.NumberStatistics == 0)
                {
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

                context.Statistics = Allocator.ShrinkUnits(context.Statistics, oldUnitCount, (uint)((context.NumberStatistics + 2) >> 1));
                context.Flags &= 0xf7;
                index = context.NumberStatistics;
                state = context.Statistics;
                context.Flags |= (byte)((state.Symbol >= 0x40) ? 0x08 : 0x00);
                do
                {
                    context.Flags |= (byte)(((++state).Symbol >= 0x40) ? 0x08 : 0x00);
                } while (--index != 0);
            }

            escapeFrequency -= (escapeFrequency >> 1);
            context.SummaryFrequency += (ushort)escapeFrequency;
            context.Flags |= 0x04;
            foundState = context.Statistics;
        }

        private void Refresh(uint oldUnitCount, bool scale, PpmContext context)
        {
            int index = context.NumberStatistics;
            int escapeFrequency;
            int scaleValue = (scale ? 1 : 0);

            context.Statistics = Allocator.ShrinkUnits(context.Statistics, oldUnitCount, (uint)((index + 2) >> 1));
            PpmState statistics = context.Statistics;
            context.Flags = (byte)((context.Flags & (0x10 + (scale ? 0x04 : 0x00))) + ((statistics.Symbol >= 0x40) ? 0x08 : 0x00));
            escapeFrequency = context.SummaryFrequency - statistics.Frequency;
            statistics.Frequency = (byte)((statistics.Frequency + scaleValue) >> scaleValue);
            context.SummaryFrequency = statistics.Frequency;

            do
            {
                escapeFrequency -= (++statistics).Frequency;
                statistics.Frequency = (byte)((statistics.Frequency + scaleValue) >> scaleValue);
                context.SummaryFrequency += statistics.Frequency;
                context.Flags |= (byte)((statistics.Symbol >= 0x40) ? 0x08 : 0x00);
            } while (--index != 0);

            escapeFrequency = (escapeFrequency + scaleValue) >> scaleValue;
            context.SummaryFrequency += (ushort)escapeFrequency;
        }

        private PpmContext CutOff(int order, PpmContext context)
        {
            int index;
            PpmState state;

            if (context.NumberStatistics == 0)
            {
                state = context.FirstState;
                if ((Pointer)state.Successor >= Allocator.BaseUnit)
                {
                    if (order < modelOrder)
                        state.Successor = CutOff(order + 1, state.Successor);
                    else
                        state.Successor = PpmContext.Zero;

                    if (state.Successor == PpmContext.Zero && order > OrderBound)
                    {
                        Allocator.SpecialFreeUnits(context);
                        return PpmContext.Zero;
                    }

                    return context;
                }
                else
                {
                    Allocator.SpecialFreeUnits(context);
                    return PpmContext.Zero;
                }
            }

            uint unitCount = (uint)((context.NumberStatistics + 2) >> 1);
            context.Statistics = Allocator.MoveUnitsUp(context.Statistics, unitCount);
            index = context.NumberStatistics;
            for (state = context.Statistics + index; state >= context.Statistics; state--)
            {
                if (state.Successor < Allocator.BaseUnit)
                {
                    state.Successor = PpmContext.Zero;
                    Swap(state, context.Statistics[index--]);
                }
                else if (order < modelOrder)
                    state.Successor = CutOff(order + 1, state.Successor);
                else
                    state.Successor = PpmContext.Zero;
            }

            if (index != context.NumberStatistics && order != 0)
            {
                context.NumberStatistics = (byte)index;
                state = context.Statistics;
                if (index < 0)
                {
                    Allocator.FreeUnits(state, unitCount);
                    Allocator.SpecialFreeUnits(context);
                    return PpmContext.Zero;
                }
                else if (index == 0)
                {
                    context.Flags = (byte)((context.Flags & 0x10) + ((state.Symbol >= 0x40) ? 0x08 : 0x00));
                    Copy(context.FirstState, state);
                    Allocator.FreeUnits(state, unitCount);
                    context.FirstStateFrequency = (byte)((context.FirstStateFrequency + 11) >> 3);
                }
                else
                {
                    Refresh(unitCount, context.SummaryFrequency > 16 * index, context);
                }
            }

            return context;
        }

        private PpmContext RemoveBinaryContexts(int order, PpmContext context)
        {
            if (context.NumberStatistics == 0)
            {
                PpmState state = context.FirstState;
                if ((Pointer)state.Successor >= Allocator.BaseUnit && order < modelOrder)
                    state.Successor = RemoveBinaryContexts(order + 1, state.Successor);
                else
                    state.Successor = PpmContext.Zero;
                if ((state.Successor == PpmContext.Zero) && (context.Suffix.NumberStatistics == 0 || context.Suffix.Flags == 0xff))
                {
                    Allocator.FreeUnits(context, 1);
                    return PpmContext.Zero;
                }
                else
                {
                    return context;
                }
            }

            for (PpmState state = context.Statistics + context.NumberStatistics; state >= context.Statistics; state--)
            {
                if ((Pointer)state.Successor >= Allocator.BaseUnit && order < modelOrder)
                    state.Successor = RemoveBinaryContexts(order + 1, state.Successor);
                else
                    state.Successor = PpmContext.Zero;
            }

            return context;
        }
    }
}
