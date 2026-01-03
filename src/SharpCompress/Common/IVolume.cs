using System;

namespace SharpCompress.Common;

/// <summary>
/// Represents a physical archive volume (a single archive file or stream).
///
/// <para>
/// This interface is distinct from <see cref="IO.IByteSource"/> in that:
/// </para>
/// <list type="bullet">
/// <item>
/// <description>
/// <b>IVolume</b> represents a physical archive container with its own
/// headers, metadata, and structure. Each volume is a complete or partial
/// archive unit.
/// </description>
/// </item>
/// <item>
/// <description>
/// <b>IByteSource</b> represents a raw source of bytes without archive-specific
/// semantics. Multiple byte sources can form a contiguous stream, or each can
/// be independent.
/// </description>
/// </item>
/// </list>
///
/// <para>
/// Archive formats use volumes differently:
/// </para>
/// <list type="bullet">
/// <item>
/// <description>
/// <b>Multi-volume RAR</b>: Each volume is an independent archive unit with
/// its own headers. Files can span volumes.
/// </description>
/// </item>
/// <item>
/// <description>
/// <b>Split ZIP</b>: Data is split across multiple files but logically forms
/// one archive. The central directory is typically in the last part.
/// </description>
/// </item>
/// <item>
/// <description>
/// <b>7Zip/TAR</b>: Typically single volume, though can be split.
/// </description>
/// </item>
/// </list>
/// </summary>
public interface IVolume : IDisposable
{
    /// <summary>
    /// Gets the zero-based index of this volume within a multi-volume archive.
    /// </summary>
    int Index { get; }

    /// <summary>
    /// Gets the file name of this volume, if it was loaded from a file.
    /// </summary>
    string? FileName { get; }
}
