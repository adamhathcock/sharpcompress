using System;
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

    /// <summary>
    /// Provide a hint for the extension of the archive being read, can speed up finding the correct decoder.  Should be without the leading period in the form like: tar.gz or zip
    /// </summary>
    public string? ExtensionHint { get; set; }

    /// <summary>
    /// An optional progress reporter for tracking extraction operations.
    /// When set, progress updates will be reported as entries are extracted.
    /// </summary>
    public IProgress<ProgressReport>? Progress { get; set; }

    /// <summary>
    /// Enable multi-threaded extraction support when the archive is opened from a FileInfo or file path.
    /// When enabled, multiple threads can extract different entries concurrently by creating
    /// independent file streams. This is only effective for archives opened from files, not streams.
    /// Default is false for backward compatibility.
    /// </summary>
    public bool EnableMultiThreadedExtraction { get; set; }
}
