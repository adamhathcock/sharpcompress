namespace SharpCompress.Compressor.LZMA
{
    using System;

    internal enum CoderPropID
    {
        DefaultProp,
        DictionarySize,
        UsedMemorySize,
        Order,
        BlockSize,
        PosStateBits,
        LitContextBits,
        LitPosBits,
        NumFastBytes,
        MatchFinder,
        MatchFinderCycles,
        NumPasses,
        Algorithm,
        NumThreads,
        EndMarker
    }
}

