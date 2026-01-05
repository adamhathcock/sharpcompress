namespace SharpCompress.Compressors.ZStandard.Unsafe;

/**
 * Huffman flags bitset.
 * For all flags, 0 is the default value.
 */
public enum HUF_flags_e
{
    /**
     * If compiled with DYNAMIC_BMI2: Set flag only if the CPU supports BMI2 at runtime.
     * Otherwise: Ignored.
     */
    HUF_flags_bmi2 = 1 << 0,

    /**
     * If set: Test possible table depths to find the one that produces the smallest header + encoded size.
     * If unset: Use heuristic to find the table depth.
     */
    HUF_flags_optimalDepth = 1 << 1,

    /**
     * If set: If the previous table can encode the input, always reuse the previous table.
     * If unset: If the previous table can encode the input, reuse the previous table if it results in a smaller output.
     */
    HUF_flags_preferRepeat = 1 << 2,

    /**
     * If set: Sample the input and check if the sample is uncompressible, if it is then don't attempt to compress.
     * If unset: Always histogram the entire input.
     */
    HUF_flags_suspectUncompressible = 1 << 3,

    /**
     * If set: Don't use assembly implementations
     * If unset: Allow using assembly implementations
     */
    HUF_flags_disableAsm = 1 << 4,

    /**
     * If set: Don't use the fast decoding loop, always use the fallback decoding loop.
     * If unset: Use the fast decoding loop when possible.
     */
    HUF_flags_disableFast = 1 << 5,
}
