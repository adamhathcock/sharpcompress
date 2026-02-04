using System;
using SharpCompress.Common;

namespace SharpCompress.Readers;

public class ReaderOptions : OptionsBase
{
    /// <summary>
    /// The default buffer size for stream operations.
    /// This value (65536 bytes) is preserved for backward compatibility.
    /// New code should use Constants.BufferSize instead (81920 bytes), which matches .NET's Stream.CopyTo default.
    /// </summary>
    [Obsolete(
        "Use Constants.BufferSize instead. This constant will be removed in a future version."
    )]
    public const int DefaultBufferSize = 0x10000;

    /// <summary>
    /// Look for RarArchive (Check for self-extracting archives or cases where RarArchive isn't at the start of the file)
    /// </summary>
    public bool LookForHeader { get; set; }

    public string? Password { get; set; }

    public bool DisableCheckIncomplete { get; set; }

    public int BufferSize { get; set; } = Constants.BufferSize;

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
    /// Size of the rewindable buffer for non-seekable streams.
    /// Used during format detection to enable multiple rewinds.
    /// </summary>
    /// <remarks>
    /// <para>
    /// When opening archives from non-seekable streams (network streams, pipes,
    /// compressed streams), SharpCompress uses a ring buffer to enable format
    /// auto-detection. This buffer allows the library to try multiple decoders
    /// by rewinding and re-reading the same data.
    /// </para>
    /// <para>
    /// <b>Default:</b> Constants.RewindableBufferSize (81920 bytes / 81KB)
    /// </para>
    /// <para>
    /// <b>Typical usage:</b> 500-1000 bytes for most archives
    /// </para>
    /// <para>
    /// <b>Increase if:</b>
    /// <list type="bullet">
    /// <item>Opening self-extracting RAR archives (may need 512KB+)</item>
    /// <item>Format detection fails with "recording anchor" errors</item>
    /// <item>Using custom formats with large headers</item>
    /// </list>
    /// </para>
    /// <para>
    /// <b>Memory impact:</b> Buffer is allocated for non-seekable streams only.
    /// Seekable streams (FileStream, MemoryStream) use zero-copy seeking instead.
    /// </para>
    /// </remarks>
    /// <example>
    /// <code>
    /// // For self-extracting archives, use larger buffer
    /// var options = new ReaderOptions
    /// {
    ///     RewindableBufferSize = 1_048_576, // 1MB
    ///     LookForHeader = true
    /// };
    /// using var reader = ReaderFactory.OpenReader(networkStream, options);
    /// </code>
    /// </example>
    public int? RewindableBufferSize { get; set; }
}
