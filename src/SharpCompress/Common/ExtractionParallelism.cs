namespace SharpCompress.Common;

/// <summary>
/// Controls whether archive extraction may use concurrent entry extraction.
/// </summary>
public enum ExtractionParallelism
{
    /// <summary>
    /// Use parallel extraction when the archive can prove it is safe; otherwise extract sequentially.
    /// </summary>
    Auto,

    /// <summary>
    /// Always use the existing sequential extraction path.
    /// </summary>
    SingleThreaded,

    /// <summary>
    /// Extract independently readable entries concurrently and fall back to sequential extraction for unsafe layouts.
    /// </summary>
    PerEntry,

    /// <summary>
    /// Require safe parallel extraction and throw when the archive layout cannot support it.
    /// </summary>
    RequireParallel,
}
