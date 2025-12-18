namespace SharpCompress.Compressors.ZStandard.Unsafe;

public enum ZSTD_nextInputType_e
{
    ZSTDnit_frameHeader,
    ZSTDnit_blockHeader,
    ZSTDnit_block,
    ZSTDnit_lastBlock,
    ZSTDnit_checksum,
    ZSTDnit_skippableFrame,
}
