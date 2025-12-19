using System.Runtime.CompilerServices;

namespace SharpCompress.Compressors.ZStandard.Unsafe;

public static unsafe partial class Methods
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool ERR_isError(nuint code)
    {
        return code > unchecked((nuint)(-(int)ZSTD_ErrorCode.ZSTD_error_maxCode));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static ZSTD_ErrorCode ERR_getErrorCode(nuint code)
    {
        if (!ERR_isError(code))
            return 0;
        return (ZSTD_ErrorCode)(0 - code);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static string ERR_getErrorName(nuint code)
    {
        return ERR_getErrorString(ERR_getErrorCode(code));
    }

    /*-****************************************
     *  Error Strings
     ******************************************/
    private static string ERR_getErrorString(ZSTD_ErrorCode code)
    {
        const string notErrorCode = "Unspecified error code";
        switch (code)
        {
            case ZSTD_ErrorCode.ZSTD_error_no_error:
                return "No error detected";
            case ZSTD_ErrorCode.ZSTD_error_GENERIC:
                return "Error (generic)";
            case ZSTD_ErrorCode.ZSTD_error_prefix_unknown:
                return "Unknown frame descriptor";
            case ZSTD_ErrorCode.ZSTD_error_version_unsupported:
                return "Version not supported";
            case ZSTD_ErrorCode.ZSTD_error_frameParameter_unsupported:
                return "Unsupported frame parameter";
            case ZSTD_ErrorCode.ZSTD_error_frameParameter_windowTooLarge:
                return "Frame requires too much memory for decoding";
            case ZSTD_ErrorCode.ZSTD_error_corruption_detected:
                return "Data corruption detected";
            case ZSTD_ErrorCode.ZSTD_error_checksum_wrong:
                return "Restored data doesn't match checksum";
            case ZSTD_ErrorCode.ZSTD_error_literals_headerWrong:
                return "Header of Literals' block doesn't respect format specification";
            case ZSTD_ErrorCode.ZSTD_error_parameter_unsupported:
                return "Unsupported parameter";
            case ZSTD_ErrorCode.ZSTD_error_parameter_combination_unsupported:
                return "Unsupported combination of parameters";
            case ZSTD_ErrorCode.ZSTD_error_parameter_outOfBound:
                return "Parameter is out of bound";
            case ZSTD_ErrorCode.ZSTD_error_init_missing:
                return "Context should be init first";
            case ZSTD_ErrorCode.ZSTD_error_memory_allocation:
                return "Allocation error : not enough memory";
            case ZSTD_ErrorCode.ZSTD_error_workSpace_tooSmall:
                return "workSpace buffer is not large enough";
            case ZSTD_ErrorCode.ZSTD_error_stage_wrong:
                return "Operation not authorized at current processing stage";
            case ZSTD_ErrorCode.ZSTD_error_tableLog_tooLarge:
                return "tableLog requires too much memory : unsupported";
            case ZSTD_ErrorCode.ZSTD_error_maxSymbolValue_tooLarge:
                return "Unsupported max Symbol Value : too large";
            case ZSTD_ErrorCode.ZSTD_error_maxSymbolValue_tooSmall:
                return "Specified maxSymbolValue is too small";
            case ZSTD_ErrorCode.ZSTD_error_cannotProduce_uncompressedBlock:
                return "This mode cannot generate an uncompressed block";
            case ZSTD_ErrorCode.ZSTD_error_stabilityCondition_notRespected:
                return "pledged buffer stability condition is not respected";
            case ZSTD_ErrorCode.ZSTD_error_dictionary_corrupted:
                return "Dictionary is corrupted";
            case ZSTD_ErrorCode.ZSTD_error_dictionary_wrong:
                return "Dictionary mismatch";
            case ZSTD_ErrorCode.ZSTD_error_dictionaryCreation_failed:
                return "Cannot create Dictionary from provided samples";
            case ZSTD_ErrorCode.ZSTD_error_dstSize_tooSmall:
                return "Destination buffer is too small";
            case ZSTD_ErrorCode.ZSTD_error_srcSize_wrong:
                return "Src size is incorrect";
            case ZSTD_ErrorCode.ZSTD_error_dstBuffer_null:
                return "Operation on NULL destination buffer";
            case ZSTD_ErrorCode.ZSTD_error_noForwardProgress_destFull:
                return "Operation made no progress over multiple calls, due to output buffer being full";
            case ZSTD_ErrorCode.ZSTD_error_noForwardProgress_inputEmpty:
                return "Operation made no progress over multiple calls, due to input being empty";
            case ZSTD_ErrorCode.ZSTD_error_frameIndex_tooLarge:
                return "Frame index is too large";
            case ZSTD_ErrorCode.ZSTD_error_seekableIO:
                return "An I/O error occurred when reading/seeking";
            case ZSTD_ErrorCode.ZSTD_error_dstBuffer_wrong:
                return "Destination buffer is wrong";
            case ZSTD_ErrorCode.ZSTD_error_srcBuffer_wrong:
                return "Source buffer is wrong";
            case ZSTD_ErrorCode.ZSTD_error_sequenceProducer_failed:
                return "Block-level external sequence producer returned an error code";
            case ZSTD_ErrorCode.ZSTD_error_externalSequences_invalid:
                return "External sequences are not valid";
            case ZSTD_ErrorCode.ZSTD_error_maxCode:
            default:
                return notErrorCode;
        }
    }
}
