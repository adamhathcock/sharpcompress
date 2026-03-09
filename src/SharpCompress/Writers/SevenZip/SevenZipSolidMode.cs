namespace SharpCompress.Writers.SevenZip;

/// <summary>
/// Controls how consecutive file writes are grouped into 7z folders.
/// </summary>
public enum SevenZipSolidMode
{
    /// <summary>
    /// Compress each file independently.
    /// </summary>
    None,

    /// <summary>
    /// Compress all consecutive non-empty files into a single folder.
    /// </summary>
    All,

    /// <summary>
    /// Group consecutive files by their normalized parent directory.
    /// </summary>
    ByDirectory,

    /// <summary>
    /// Group consecutive files by file extension.
    /// </summary>
    ByExtension,

    /// <summary>
    /// Group consecutive files by a custom selector.
    /// </summary>
    Custom,
}
