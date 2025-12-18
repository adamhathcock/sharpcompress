namespace SharpCompress.Compressors.ZStandard.Unsafe;

public enum BIT_DStream_status
{
    /* fully refilled */
    BIT_DStream_unfinished = 0,

    /* still some bits left in bitstream */
    BIT_DStream_endOfBuffer = 1,

    /* bitstream entirely consumed, bit-exact */
    BIT_DStream_completed = 2,

    /* user requested more bits than present in bitstream */
    BIT_DStream_overflow = 3,
}
