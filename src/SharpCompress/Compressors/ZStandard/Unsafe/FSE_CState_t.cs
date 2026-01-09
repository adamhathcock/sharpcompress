namespace SharpCompress.Compressors.ZStandard.Unsafe;

/* *****************************************
 *  FSE symbol compression API
 *******************************************/
/*!
This API consists of small unitary functions, which highly benefit from being inlined.
Hence their body are included in next section.
 */
public unsafe struct FSE_CState_t
{
    public nint value;
    public void* stateTable;
    public void* symbolTT;
    public uint stateLog;
}
