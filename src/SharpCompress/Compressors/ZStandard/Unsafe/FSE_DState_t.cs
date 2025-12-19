namespace SharpCompress.Compressors.ZStandard.Unsafe;

/* *****************************************
 *  FSE symbol decompression API
 *******************************************/
public unsafe struct FSE_DState_t
{
    public nuint state;

    /* precise table may vary, depending on U16 */
    public void* table;
}
