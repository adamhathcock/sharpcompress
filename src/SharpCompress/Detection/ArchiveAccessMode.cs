using System;

namespace SharpCompress.Detection;

/// <summary>
/// Specifies the APIs available for an archive format.
/// </summary>
[Flags]
public enum ArchiveAccessMode
{
    /// <summary>
    /// No archive access API is available.
    /// </summary>
    None = 0,

    /// <summary>
    /// The seekable <see cref="IArchive"/> API is available.
    /// </summary>
    Archive = 1,

    /// <summary>
    /// The forward-only <see cref="SharpCompress.Readers.IReader"/> API is available.
    /// </summary>
    Reader = 2,
}
