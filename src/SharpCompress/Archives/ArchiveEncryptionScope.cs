using System;

namespace SharpCompress.Archives;

/// <summary>
/// Identifies which archive data is encrypted.
/// </summary>
[Flags]
public enum ArchiveEncryptionScope
{
    /// <summary>
    /// No encryption is present.
    /// </summary>
    None = 0,

    /// <summary>
    /// One or more entry payloads are encrypted.
    /// </summary>
    EntryData = 1,

    /// <summary>
    /// Archive headers are encrypted.
    /// </summary>
    Headers = 2,
}
