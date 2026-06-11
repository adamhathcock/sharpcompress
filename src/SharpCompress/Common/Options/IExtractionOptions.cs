using System;

namespace SharpCompress.Common.Options;

/// <summary>
/// Options for configuring extraction behavior when extracting archive entries to the filesystem.
/// </summary>
public interface IExtractionOptions
{
    /// <summary>
    /// Overwrite target if it exists.
    /// <para><b>Breaking change:</b> Default changed from false to true in version 0.40.0.</para>
    /// </summary>
    bool Overwrite { get; set; }

    /// <summary>
    /// Extract with internal directory structure.
    /// <para><b>Breaking change:</b> Default changed from false to true in version 0.40.0.</para>
    /// </summary>
    bool ExtractFullPath { get; set; }

    /// <summary>
    /// Preserve file time.
    /// <para><b>Breaking change:</b> Default changed from false to true in version 0.40.0.</para>
    /// </summary>
    bool PreserveFileTime { get; set; }

    /// <summary>
    /// Preserve windows file attributes.
    /// </summary>
    bool PreserveAttributes { get; set; }

    /// <summary>
    /// Buffer size for extraction stream copy operations.
    /// </summary>
    int BufferSize { get; set; }

    /// <summary>
    /// Delegate for writing symbolic links to disk.
    /// The first parameter is the source path (where the symlink is created).
    /// The second parameter is the target path (what the symlink refers to).
    /// </summary>
    Action<string, string>? SymbolicLinkHandler { get; set; }
}
