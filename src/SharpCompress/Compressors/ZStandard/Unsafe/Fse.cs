using System.Runtime.CompilerServices;
using static SharpCompress.Compressors.ZStandard.UnsafeHelper;

namespace SharpCompress.Compressors.ZStandard.Unsafe;

public static unsafe partial class Methods
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void FSE_initCState(FSE_CState_t* statePtr, uint* ct)
    {
        void* ptr = ct;
        ushort* u16ptr = (ushort*)ptr;
        uint tableLog = MEM_read16(ptr);
        statePtr->value = (nint)1 << (int)tableLog;
        statePtr->stateTable = u16ptr + 2;
        statePtr->symbolTT = ct + 1 + (tableLog != 0 ? 1 << (int)(tableLog - 1) : 1);
        statePtr->stateLog = tableLog;
    }

    /*! FSE_initCState2() :
     *   Same as FSE_initCState(), but the first symbol to include (which will be the last to be read)
     *   uses the smallest state value possible, saving the cost of this symbol */
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void FSE_initCState2(ref FSE_CState_t statePtr, uint* ct, uint symbol)
    {
        FSE_initCState(ref statePtr, ct);
        {
            FSE_symbolCompressionTransform symbolTT = (
                (FSE_symbolCompressionTransform*)statePtr.symbolTT
            )[symbol];
            ushort* stateTable = (ushort*)statePtr.stateTable;
            uint nbBitsOut = symbolTT.deltaNbBits + (1 << 15) >> 16;
            statePtr.value = (nint)((nbBitsOut << 16) - symbolTT.deltaNbBits);
            statePtr.value = stateTable[
                (statePtr.value >> (int)nbBitsOut) + symbolTT.deltaFindState
            ];
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void FSE_encodeSymbol(
        ref nuint bitC_bitContainer,
        ref uint bitC_bitPos,
        ref FSE_CState_t statePtr,
        uint symbol
    )
    {
        FSE_symbolCompressionTransform symbolTT = (
            (FSE_symbolCompressionTransform*)statePtr.symbolTT
        )[symbol];
        ushort* stateTable = (ushort*)statePtr.stateTable;
        uint nbBitsOut = (uint)statePtr.value + symbolTT.deltaNbBits >> 16;
        BIT_addBits(ref bitC_bitContainer, ref bitC_bitPos, (nuint)statePtr.value, nbBitsOut);
        statePtr.value = stateTable[(statePtr.value >> (int)nbBitsOut) + symbolTT.deltaFindState];
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void FSE_flushCState(
        ref nuint bitC_bitContainer,
        ref uint bitC_bitPos,
        ref sbyte* bitC_ptr,
        sbyte* bitC_endPtr,
        ref FSE_CState_t statePtr
    )
    {
        BIT_addBits(
            ref bitC_bitContainer,
            ref bitC_bitPos,
            (nuint)statePtr.value,
            statePtr.stateLog
        );
        BIT_flushBits(ref bitC_bitContainer, ref bitC_bitPos, ref bitC_ptr, bitC_endPtr);
    }

    /* FSE_getMaxNbBits() :
     * Approximate maximum cost of a symbol, in bits.
     * Fractional get rounded up (i.e. a symbol with a normalized frequency of 3 gives the same result as a frequency of 2)
     * note 1 : assume symbolValue is valid (<= maxSymbolValue)
     * note 2 : if freq[symbolValue]==0, @return a fake cost of tableLog+1 bits */
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static uint FSE_getMaxNbBits(void* symbolTTPtr, uint symbolValue)
    {
        FSE_symbolCompressionTransform* symbolTT = (FSE_symbolCompressionTransform*)symbolTTPtr;
        return symbolTT[symbolValue].deltaNbBits + ((1 << 16) - 1) >> 16;
    }

    /* FSE_bitCost() :
     * Approximate symbol cost, as fractional value, using fixed-point format (accuracyLog fractional bits)
     * note 1 : assume symbolValue is valid (<= maxSymbolValue)
     * note 2 : if freq[symbolValue]==0, @return a fake cost of tableLog+1 bits */
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static uint FSE_bitCost(
        void* symbolTTPtr,
        uint tableLog,
        uint symbolValue,
        uint accuracyLog
    )
    {
        FSE_symbolCompressionTransform* symbolTT = (FSE_symbolCompressionTransform*)symbolTTPtr;
        uint minNbBits = symbolTT[symbolValue].deltaNbBits >> 16;
        uint threshold = minNbBits + 1 << 16;
        assert(tableLog < 16);
        assert(accuracyLog < 31 - tableLog);
        {
            uint tableSize = (uint)(1 << (int)tableLog);
            uint deltaFromThreshold = threshold - (symbolTT[symbolValue].deltaNbBits + tableSize);
            /* linear interpolation (very approximate) */
            uint normalizedDeltaFromThreshold =
                deltaFromThreshold << (int)accuracyLog >> (int)tableLog;
            uint bitMultiplier = (uint)(1 << (int)accuracyLog);
            assert(symbolTT[symbolValue].deltaNbBits + tableSize <= threshold);
            assert(normalizedDeltaFromThreshold <= bitMultiplier);
            return (minNbBits + 1) * bitMultiplier - normalizedDeltaFromThreshold;
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void FSE_initDState(ref FSE_DState_t DStatePtr, ref BIT_DStream_t bitD, uint* dt)
    {
        void* ptr = dt;
        FSE_DTableHeader* DTableH = (FSE_DTableHeader*)ptr;
        DStatePtr.state = BIT_readBits(bitD.bitContainer, ref bitD.bitsConsumed, DTableH->tableLog);
        BIT_reloadDStream(
            ref bitD.bitContainer,
            ref bitD.bitsConsumed,
            ref bitD.ptr,
            bitD.start,
            bitD.limitPtr
        );
        DStatePtr.table = dt + 1;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static byte FSE_peekSymbol(FSE_DState_t* DStatePtr)
    {
        FSE_decode_t DInfo = ((FSE_decode_t*)DStatePtr->table)[DStatePtr->state];
        return DInfo.symbol;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void FSE_updateState(FSE_DState_t* DStatePtr, BIT_DStream_t* bitD)
    {
        FSE_decode_t DInfo = ((FSE_decode_t*)DStatePtr->table)[DStatePtr->state];
        uint nbBits = DInfo.nbBits;
        nuint lowBits = BIT_readBits(bitD, nbBits);
        DStatePtr->state = DInfo.newState + lowBits;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static byte FSE_decodeSymbol(
        ref FSE_DState_t DStatePtr,
        nuint bitD_bitContainer,
        ref uint bitD_bitsConsumed
    )
    {
        FSE_decode_t DInfo = ((FSE_decode_t*)DStatePtr.table)[DStatePtr.state];
        uint nbBits = DInfo.nbBits;
        byte symbol = DInfo.symbol;
        nuint lowBits = BIT_readBits(bitD_bitContainer, ref bitD_bitsConsumed, nbBits);
        DStatePtr.state = DInfo.newState + lowBits;
        return symbol;
    }

    /*! FSE_decodeSymbolFast() :
    unsafe, only works if no symbol has a probability > 50% */
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static byte FSE_decodeSymbolFast(
        ref FSE_DState_t DStatePtr,
        nuint bitD_bitContainer,
        ref uint bitD_bitsConsumed
    )
    {
        FSE_decode_t DInfo = ((FSE_decode_t*)DStatePtr.table)[DStatePtr.state];
        uint nbBits = DInfo.nbBits;
        byte symbol = DInfo.symbol;
        nuint lowBits = BIT_readBitsFast(bitD_bitContainer, ref bitD_bitsConsumed, nbBits);
        DStatePtr.state = DInfo.newState + lowBits;
        return symbol;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static uint FSE_endOfDState(FSE_DState_t* DStatePtr)
    {
        return DStatePtr->state == 0 ? 1U : 0U;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void FSE_initCState(ref FSE_CState_t statePtr, uint* ct)
    {
        void* ptr = ct;
        ushort* u16ptr = (ushort*)ptr;
        uint tableLog = MEM_read16(ptr);
        statePtr.value = (nint)1 << (int)tableLog;
        statePtr.stateTable = u16ptr + 2;
        statePtr.symbolTT = ct + 1 + (tableLog != 0 ? 1 << (int)(tableLog - 1) : 1);
        statePtr.stateLog = tableLog;
    }
}
