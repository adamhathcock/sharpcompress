namespace SharpCompress.Compressors.ZStandard.Unsafe;

/*-*************************************
 *  Context memory management
 ***************************************/
public enum ZSTD_compressionStage_e
{
    ZSTDcs_created = 0,
    ZSTDcs_init,
    ZSTDcs_ongoing,
    ZSTDcs_ending,
}
