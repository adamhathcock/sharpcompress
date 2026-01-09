namespace SharpCompress.Compressors.ZStandard.Unsafe;

/* Struct to keep track of where we are in our recursive calls. */
public unsafe struct seqStoreSplits
{
    /* Array of split indices */
    public uint* splitLocations;

    /* The current index within splitLocations being worked on */
    public nuint idx;
}
