using SharpCompress.Common;

namespace SharpCompress.Readers;

public class ReaderOptions : OptionsBase
{
    public const int DefaultBufferSize = 0x10000;

    /// <summary>
    /// Look for RarArchive (Check for self-extracting archives or cases where RarArchive isn't at the start of the file)
    /// </summary>
    public bool LookForHeader { get; set; }

    public string? Password { get; set; }

    public bool DisableCheckIncomplete { get; set; }

    public int BufferSize { get; set; } = DefaultBufferSize;
}
