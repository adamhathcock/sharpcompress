using System;

namespace SharpCompress.Detection;

/// <summary>
/// Identifies conditions that prevented complete archive metadata inspection.
/// </summary>
[Flags]
public enum ArchiveInformationLimitations
{
    /// <summary>
    /// No known inspection limitations apply.
    /// </summary>
    None = 0,

    /// <summary>
    /// Archive headers are encrypted and no password was supplied.
    /// </summary>
    EncryptedHeaders = 1,

    /// <summary>
    /// One or more archive volumes are unavailable.
    /// </summary>
    MissingVolumes = 2,

    /// <summary>
    /// The format does not expose a requested metadata value.
    /// </summary>
    UnavailableMetadata = 4,
}
