namespace SharpCompress.Compressors.ZStandard.Unsafe;

public struct ZDICT_fastCover_params_t
{
    /* Segment size : constraint: 0 < k : Reasonable range [16, 2048+] */
    public uint k;

    /* dmer size : constraint: 0 < d <= k : Reasonable range [6, 16] */
    public uint d;

    /* log of size of frequency array : constraint: 0 < f <= 31 : 1 means default(20)*/
    public uint f;

    /* Number of steps : Only used for optimization : 0 means default (40) : Higher means more parameters checked */
    public uint steps;

    /* Number of threads : constraint: 0 < nbThreads : 1 means single-threaded : Only used for optimization : Ignored if ZSTD_MULTITHREAD is not defined */
    public uint nbThreads;

    /* Percentage of samples used for training: Only used for optimization : the first nbSamples * splitPoint samples will be used to training, the last nbSamples * (1 - splitPoint) samples will be used for testing, 0 means default (0.75), 1.0 when all samples are used for both training and testing */
    public double splitPoint;

    /* Acceleration level: constraint: 0 < accel <= 10, higher means faster and less accurate, 0 means default(1) */
    public uint accel;

    /* Train dictionaries to shrink in size starting from the minimum size and selects the smallest dictionary that is shrinkDictMaxRegression% worse than the largest dictionary. 0 means no shrinking and 1 means shrinking  */
    public uint shrinkDict;

    /* Sets shrinkDictMaxRegression so that a smaller dictionary can be at worse shrinkDictMaxRegression% worse than the max dict size dictionary. */
    public uint shrinkDictMaxRegression;
    public ZDICT_params_t zParams;
}
