namespace SharpCompress.Compressors.ZStandard.Unsafe;

/*-*************************************
 * Acceleration
 ***************************************/
public struct FASTCOVER_accel_t
{
    /* Percentage of training samples used for ZDICT_finalizeDictionary */
    public uint finalize;

    /* Number of dmer skipped between each dmer counted in computeFrequency */
    public uint skip;

    public FASTCOVER_accel_t(uint finalize, uint skip)
    {
        this.finalize = finalize;
        this.skip = skip;
    }
}
