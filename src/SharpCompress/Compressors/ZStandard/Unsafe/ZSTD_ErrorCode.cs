namespace SharpCompress.Compressors.ZStandard.Unsafe;

/*-*********************************************
 *  Error codes list
 *-*********************************************
 *  Error codes _values_ are pinned down since v1.3.1 only.
 *  Therefore, don't rely on values if you may link to any version < v1.3.1.
 *
 *  Only values < 100 are considered stable.
 *
 *  note 1 : this API shall be used with static linking only.
 *           dynamic linking is not yet officially supported.
 *  note 2 : Prefer relying on the enum than on its value whenever possible
 *           This is the only supported way to use the error list < v1.3.1
 *  note 3 : ZSTD_isError() is always correct, whatever the library version.
 **********************************************/
public enum ZSTD_ErrorCode
{
    ZSTD_error_no_error = 0,
    ZSTD_error_GENERIC = 1,
    ZSTD_error_prefix_unknown = 10,
    ZSTD_error_version_unsupported = 12,
    ZSTD_error_frameParameter_unsupported = 14,
    ZSTD_error_frameParameter_windowTooLarge = 16,
    ZSTD_error_corruption_detected = 20,
    ZSTD_error_checksum_wrong = 22,
    ZSTD_error_literals_headerWrong = 24,
    ZSTD_error_dictionary_corrupted = 30,
    ZSTD_error_dictionary_wrong = 32,
    ZSTD_error_dictionaryCreation_failed = 34,
    ZSTD_error_parameter_unsupported = 40,
    ZSTD_error_parameter_combination_unsupported = 41,
    ZSTD_error_parameter_outOfBound = 42,
    ZSTD_error_tableLog_tooLarge = 44,
    ZSTD_error_maxSymbolValue_tooLarge = 46,
    ZSTD_error_maxSymbolValue_tooSmall = 48,
    ZSTD_error_cannotProduce_uncompressedBlock = 49,
    ZSTD_error_stabilityCondition_notRespected = 50,
    ZSTD_error_stage_wrong = 60,
    ZSTD_error_init_missing = 62,
    ZSTD_error_memory_allocation = 64,
    ZSTD_error_workSpace_tooSmall = 66,
    ZSTD_error_dstSize_tooSmall = 70,
    ZSTD_error_srcSize_wrong = 72,
    ZSTD_error_dstBuffer_null = 74,
    ZSTD_error_noForwardProgress_destFull = 80,
    ZSTD_error_noForwardProgress_inputEmpty = 82,

    /* following error codes are __NOT STABLE__, they can be removed or changed in future versions */
    ZSTD_error_frameIndex_tooLarge = 100,
    ZSTD_error_seekableIO = 102,
    ZSTD_error_dstBuffer_wrong = 104,
    ZSTD_error_srcBuffer_wrong = 105,
    ZSTD_error_sequenceProducer_failed = 106,
    ZSTD_error_externalSequences_invalid = 107,

    /* never EVER use this value directly, it can change in future versions! Use ZSTD_isError() instead */
    ZSTD_error_maxCode = 120,
}
