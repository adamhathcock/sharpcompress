using SharpCompress.Common;

namespace SharpCompress.Common.Options;

/// <summary>
/// Options for controlling concurrent extraction behavior.
/// </summary>
public interface IExtractionParallelismOptions
{
    /// <summary>
    /// Controls whether extraction may run entries concurrently when the archive format and backing stream are safe.
    /// </summary>
    ExtractionParallelism Parallelism { get; set; }

    /// <summary>
    /// Maximum number of concurrent extraction workers used when parallel extraction is selected.
    /// </summary>
    int MaxDegreeOfParallelism { get; set; }
}
