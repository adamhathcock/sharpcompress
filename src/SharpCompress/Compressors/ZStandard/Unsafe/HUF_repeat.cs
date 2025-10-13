namespace SharpCompress.Compressors.ZStandard.Unsafe;

public enum HUF_repeat
{
    /**< Cannot use the previous table */
    HUF_repeat_none,

    /**< Can use the previous table but it must be checked. Note : The previous table must have been constructed by HUF_compress{1, 4}X_repeat */
    HUF_repeat_check,

    /**< Can use the previous table and it is assumed to be valid */
    HUF_repeat_valid,
}
