namespace SharpCompress.Compressors.ZStandard;

internal class ZstandardConstants
{
    /// <summary>
    /// Magic number found at start of ZStandard frame: 0xFD 0x2F 0xB5 0x28
    /// </summary>
    public const uint MAGIC = 0xFD2FB528;
}
