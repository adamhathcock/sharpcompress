namespace SharpCompress.Compressors.ZStandard.Unsafe;

public enum FSE_repeat
{
    /**< Cannot use the previous table */
    FSE_repeat_none,

    /**< Can use the previous table but it must be checked */
    FSE_repeat_check,

    /**< Can use the previous table and it is assumed to be valid */
    FSE_repeat_valid,
}
