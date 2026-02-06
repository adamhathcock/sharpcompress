using System;

namespace SharpCompress.Common;

/// <summary>
/// Options for configuring extraction behavior when extracting archive entries.
/// </summary>
/// <remarks>
/// This class is immutable. Use the <c>with</c> expression to create modified copies:
/// <code>
/// var options = new ExtractionOptions { Overwrite = false };
/// options = options with { PreserveFileTime = true };
/// </code>
/// </remarks>
public sealed record ExtractionOptions
{
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

    /// <summary>
    /// Creates a new ExtractionOptions instance with default values.
    /// </summary>
    public ExtractionOptions() { }

    /// <summary>
    /// Creates a new ExtractionOptions instance with the specified overwrite behavior.
    /// </summary>
    /// <param name="overwrite">Whether to overwrite existing files.</param>
    public ExtractionOptions(bool overwrite)
    {
        Overwrite = overwrite;
    }

    /// <summary>
    /// Creates a new ExtractionOptions instance with the specified extraction path and overwrite behavior.
    /// </summary>
    /// <param name="extractFullPath">Whether to preserve directory structure.</param>
    /// <param name="overwrite">Whether to overwrite existing files.</param>
    public ExtractionOptions(bool extractFullPath, bool overwrite)
    {
        ExtractFullPath = extractFullPath;
        Overwrite = overwrite;
    }

    /// <summary>
    /// Creates a new ExtractionOptions instance with the specified extraction path, overwrite behavior, and file time preservation.
    /// </summary>
    /// <param name="extractFullPath">Whether to preserve directory structure.</param>
    /// <param name="overwrite">Whether to overwrite existing files.</param>
    /// <param name="preserveFileTime">Whether to preserve file modification times.</param>
    public ExtractionOptions(bool extractFullPath, bool overwrite, bool preserveFileTime)
    {
        ExtractFullPath = extractFullPath;
        Overwrite = overwrite;
        PreserveFileTime = preserveFileTime;
    }

    /// <summary>
    /// Gets an ExtractionOptions instance configured for safe extraction (no overwrite).
    /// </summary>
    public static ExtractionOptions SafeExtract => new(overwrite: false);

    /// <summary>
    /// Gets an ExtractionOptions instance configured for flat extraction (no directory structure).
    /// </summary>
    public static ExtractionOptions FlatExtract => new(extractFullPath: false, overwrite: true);

    /// <summary>
    /// Default symbolic link handler that logs a warning message.
    /// </summary>
    public static void DefaultSymbolicLinkHandler(string sourcePath, string targetPath)
    {
        Console.WriteLine(
            $"Could not write symlink {sourcePath} -> {targetPath}, for more information please see https://github.com/dotnet/runtime/issues/24271"
        );
    }
}
