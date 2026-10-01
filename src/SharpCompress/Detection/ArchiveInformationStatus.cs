namespace SharpCompress.Detection;

/// <summary>
/// Describes whether archive metadata could be collected completely.
/// </summary>
public enum ArchiveInformationStatus
{
    /// <summary>
    /// All metadata supported by the format was collected.
    /// </summary>
    Complete,

    /// <summary>
    /// Some metadata could not be collected. See <see cref="ArchiveInformation.Limitations"/>.
    /// </summary>
    Partial,
}
