namespace SharpCompress.Compressors.ZStandard.Unsafe;

public unsafe struct ZSTD_DCtx_s
{
    public ZSTD_seqSymbol* LLTptr;
    public ZSTD_seqSymbol* MLTptr;
    public ZSTD_seqSymbol* OFTptr;
    public uint* HUFptr;
    public ZSTD_entropyDTables_t entropy;

    /* space needed when building huffman tables */
    public fixed uint workspace[640];

    /* detect continuity */
    public void* previousDstEnd;

    /* start of current segment */
    public void* prefixStart;

    /* virtual start of previous segment if it was just before current one */
    public void* virtualStart;

    /* end of previous segment */
    public void* dictEnd;
    public nuint expected;
    public ZSTD_frameHeader fParams;
    public ulong processedCSize;
    public ulong decodedSize;

    /* used in ZSTD_decompressContinue(), store blockType between block header decoding and block decompression stages */
    public blockType_e bType;
    public ZSTD_dStage stage;
    public uint litEntropy;
    public uint fseEntropy;
    public XXH64_state_s xxhState;
    public nuint headerSize;
    public ZSTD_format_e format;

    /* User specified: if == 1, will ignore checksums in compressed frame. Default == 0 */
    public ZSTD_forceIgnoreChecksum_e forceIgnoreChecksum;

    /* if == 1, will validate checksum. Is == 1 if (fParams.checksumFlag == 1) and (forceIgnoreChecksum == 0). */
    public uint validateChecksum;
    public byte* litPtr;
    public ZSTD_customMem customMem;
    public nuint litSize;
    public nuint rleSize;
    public nuint staticSize;
    public int isFrameDecompression;

    /* dictionary */
    public ZSTD_DDict_s* ddictLocal;

    /* set by ZSTD_initDStream_usingDDict(), or ZSTD_DCtx_refDDict() */
    public ZSTD_DDict_s* ddict;
    public uint dictID;

    /* if == 1 : dictionary is "new" for working context, and presumed "cold" (not in cpu cache) */
    public int ddictIsCold;
    public ZSTD_dictUses_e dictUses;

    /* Hash set for multiple ddicts */
    public ZSTD_DDictHashSet* ddictSet;

    /* User specified: if == 1, will allow references to multiple DDicts. Default == 0 (disabled) */
    public ZSTD_refMultipleDDicts_e refMultipleDDicts;
    public int disableHufAsm;
    public int maxBlockSizeParam;

    /* streaming */
    public ZSTD_dStreamStage streamStage;
    public sbyte* inBuff;
    public nuint inBuffSize;
    public nuint inPos;
    public nuint maxWindowSize;
    public sbyte* outBuff;
    public nuint outBuffSize;
    public nuint outStart;
    public nuint outEnd;
    public nuint lhSize;
    public uint hostageByte;
    public int noForwardProgress;
    public ZSTD_bufferMode_e outBufferMode;
    public ZSTD_outBuffer_s expectedOutBuffer;

    /* workspace */
    public byte* litBuffer;
    public byte* litBufferEnd;
    public ZSTD_litLocation_e litBufferLocation;

    /* literal buffer can be split between storage within dst and within this scratch buffer */
    public fixed byte litExtraBuffer[65568];
    public fixed byte headerBuffer[18];
    public nuint oversizedDuration;
}
