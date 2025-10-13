namespace SharpCompress.Compressors.ZStandard.Unsafe;

/**
 * Controls, for this matchState reset, whether the tables need to be cleared /
 * prepared for the coming compression (ZSTDcrp_makeClean), or whether the
 * tables can be left unclean (ZSTDcrp_leaveDirty), because we know that a
 * subsequent operation will overwrite the table space anyways (e.g., copying
 * the matchState contents in from a CDict).
 */
public enum ZSTD_compResetPolicy_e
{
    ZSTDcrp_makeClean,
    ZSTDcrp_leaveDirty,
}
