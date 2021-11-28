using System;

namespace ZstdSharp.Unsafe
{
    /*-*************************************
    * Acceleration
    ***************************************/
    public partial struct FASTCOVER_accel_t
    {
        /* Percentage of training samples used for ZDICT_finalizeDictionary */
        public uint finalize;

        /* Number of dmer skipped between each dmer counted in computeFrequency */
        public uint skip;
    }
}
