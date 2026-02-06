using System;
using SharpCompress.Common;
using SharpCompress.Common.Options;

namespace SharpCompress.Readers;

/// <summary>
/// Options for configuring reader behavior when opening archives.
/// </summary>
/// <remarks>
/// This class is immutable. Use the <c>with</c> expression to create modified copies:
/// <code>
/// var options = new ReaderOptions { Password = "secret" };
/// options = options with { LeaveStreamOpen = false };
/// </code>
/// </remarks>
public sealed record ReaderOptions : IReaderOptions
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
    /// SharpCompress will keep the supplied streams open.  Default is true.
    /// </summary>
    public bool LeaveStreamOpen { get; init; } = true;

    /// <summary>
    /// Encoding to use for archive entry names.
    /// </summary>
    public IArchiveEncoding ArchiveEncoding { get; init; } = new ArchiveEncoding();

    /// <summary>
    /// Look for RarArchive (Check for self-extracting archives or cases where RarArchive isn't at the start of the file)
    /// </summary>
    public bool LookForHeader { get; init; }

    /// <summary>
    /// Password for encrypted archives.
    /// </summary>
    public string? Password { get; init; }

    /// <summary>
    /// Disable checking for incomplete archives.
    /// </summary>
    public bool DisableCheckIncomplete { get; init; }

    /// <summary>
    /// Buffer size for stream operations.
    /// </summary>
    public int BufferSize { get; init; } = Constants.BufferSize;

    /// <summary>
    /// Provide a hint for the extension of the archive being read, can speed up finding the correct decoder.  Should be without the leading period in the form like: tar.gz or zip
    /// </summary>
    public string? ExtensionHint { get; init; }

    /// <summary>
    /// An optional progress reporter for tracking extraction operations.
    /// When set, progress updates will be reported as entries are extracted.
    /// </summary>
    public IProgress<ProgressReport>? Progress { get; init; }

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
    public int? RewindableBufferSize { get; init; }

    /// <summary>
    /// Creates a new ReaderOptions instance with default values.
    /// </summary>
    public ReaderOptions() { }

    /// <summary>
    /// Creates a new ReaderOptions instance with the specified password.
    /// </summary>
    /// <param name="password">The password for encrypted archives.</param>
    public ReaderOptions(string? password) => Password = password;

    /// <summary>
    /// Creates a new ReaderOptions instance with the specified password and header search option.
    /// </summary>
    /// <param name="password">The password for encrypted archives.</param>
    /// <param name="lookForHeader">Whether to search for the archive header.</param>
    public ReaderOptions(string? password, bool lookForHeader)
    {
        Password = password;
        LookForHeader = lookForHeader;
    }

    /// <summary>
    /// Creates a new ReaderOptions instance with the specified encoding.
    /// </summary>
    /// <param name="encoding">The encoding for archive entry names.</param>
    public ReaderOptions(IArchiveEncoding encoding) => ArchiveEncoding = encoding;

    /// <summary>
    /// Creates a new ReaderOptions instance with the specified password and encoding.
    /// </summary>
    /// <param name="password">The password for encrypted archives.</param>
    /// <param name="encoding">The encoding for archive entry names.</param>
    public ReaderOptions(string? password, IArchiveEncoding encoding)
    {
        Password = password;
        ArchiveEncoding = encoding;
    }

    /// <summary>
    /// Creates a new ReaderOptions instance with the specified stream open behavior.
    /// </summary>
    /// <param name="leaveStreamOpen">Whether to leave the stream open after reading.</param>
    public ReaderOptions(bool leaveStreamOpen)
    {
        LeaveStreamOpen = leaveStreamOpen;
    }

    /// <summary>
    /// Creates a new ReaderOptions instance with the specified stream open behavior and password.
    /// </summary>
    /// <param name="leaveStreamOpen">Whether to leave the stream open after reading.</param>
    /// <param name="password">The password for encrypted archives.</param>
    public ReaderOptions(bool leaveStreamOpen, string? password)
    {
        LeaveStreamOpen = leaveStreamOpen;
        Password = password;
    }

    /// <summary>
    /// Creates a new ReaderOptions instance with the specified buffer size.
    /// </summary>
    /// <param name="bufferSize">The buffer size for stream operations.</param>
    public ReaderOptions(int bufferSize)
    {
        BufferSize = bufferSize;
    }
}
