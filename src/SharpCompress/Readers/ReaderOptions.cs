using System;
using SharpCompress.Common;
using SharpCompress.Common.Options;
using SharpCompress.Compressors;

namespace SharpCompress.Readers;

/// <summary>
/// Options for configuring reader behavior when opening archives.
/// </summary>
/// <remarks>
/// This class is immutable. Use factory presets and fluent helpers for common configurations:
/// <code>
/// var options = ReaderOptions.ForExternalStream()
///     .WithPassword("secret")
///     .WithLookForHeader(true);
/// </code>
/// Or use object initializers for simple cases:
/// <code>
/// var options = new ReaderOptions { Password = "secret", LeaveStreamOpen = false };
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
    /// Overwrite target if it exists.
    /// <para><b>Breaking change:</b> Default changed from false to true in version 0.40.0.</para>
    /// </summary>
    public bool Overwrite { get; init; } = true;

    /// <summary>
    /// Extract with internal directory structure.
    /// <para><b>Breaking change:</b> Default changed from false to true in version 0.40.0.</para>
    /// </summary>
    public bool ExtractFullPath { get; init; } = true;

    /// <summary>
    /// Preserve file time.
    /// <para><b>Breaking change:</b> Default changed from false to true in version 0.40.0.</para>
    /// </summary>
    public bool PreserveFileTime { get; init; } = true;

    /// <summary>
    /// Preserve windows file attributes.
    /// </summary>
    public bool PreserveAttributes { get; init; }

    /// <summary>
    /// Delegate for writing symbolic links to disk.
    /// The first parameter is the source path (where the symlink is created).
    /// The second parameter is the target path (what the symlink refers to).
    /// </summary>
    /// <remarks>
    /// <b>Breaking change:</b> Changed from field to init-only property in version 0.40.0.
    /// The default handler logs a warning message.
    /// </remarks>
    public Action<string, string>? SymbolicLinkHandler { get; init; }

    /// Registry of compression providers.
    /// Defaults to <see cref="CompressionProviderRegistry.Default" /> but can be replaced with custom implementations, such as
    /// System.IO.Compression for Deflate/GZip on modern .NET.
    /// </summary>
    public CompressionProviderRegistry CompressionProviders { get; init; } =
        CompressionProviderRegistry.Default;

    /// <summary>
    /// Creates a new ReaderOptions instance with default values.
    /// </summary>
    public ReaderOptions() { }

    /// <summary>
    /// Gets ReaderOptions configured for caller-provided streams.
    /// </summary>
    public static ReaderOptions ForExternalStream => new() { LeaveStreamOpen = true };

    /// <summary>
    /// Gets ReaderOptions configured for file-based overloads that open their own stream.
    /// </summary>
    public static ReaderOptions ForOwnedFile => new() { LeaveStreamOpen = false };

    /// <summary>
    /// Gets a ReaderOptions instance configured for safe extraction (no overwrite).
    /// </summary>
    public static ReaderOptions SafeExtract => new() { Overwrite = false };

    /// <summary>
    /// Gets a ReaderOptions instance configured for flat extraction (no directory structure).
    /// </summary>
    public static ReaderOptions FlatExtract => new() { ExtractFullPath = false, Overwrite = true };

    /// <summary>
    /// Creates ReaderOptions for reading encrypted archives.
    /// </summary>
    /// <param name="password">The password for encrypted archives.</param>
    public static ReaderOptions ForEncryptedArchive(string? password = null) =>
        new ReaderOptions().WithPassword(password);

    /// <summary>
    /// Creates ReaderOptions for archives with custom character encoding.
    /// </summary>
    /// <param name="encoding">The encoding for archive entry names.</param>
    public static ReaderOptions ForEncoding(IArchiveEncoding encoding) =>
        new ReaderOptions().WithArchiveEncoding(encoding);

    /// <summary>
    /// Creates ReaderOptions for self-extracting archives that require header search.
    /// </summary>
    public static ReaderOptions ForSelfExtractingArchive(string? password = null) =>
        new ReaderOptions()
            .WithLookForHeader(true)
            .WithPassword(password)
            .WithRewindableBufferSize(1_048_576); // 1MB for SFX archives

    /// <summary>
    /// Default symbolic link handler that logs a warning message.
    /// </summary>
    public static void DefaultSymbolicLinkHandler(string sourcePath, string targetPath)
    {
        Console.WriteLine(
            $"Could not write symlink {sourcePath} -> {targetPath}, for more information please see https://github.com/dotnet/runtime/issues/24271"
        );
    }

    // Note: Parameterized constructors have been removed.
    // Use fluent With*() helpers or object initializers instead:
    // new ReaderOptions().WithPassword("secret").WithLookForHeader(true)
    // or
    // new ReaderOptions { Password = "secret", LookForHeader = true }
}
