namespace SharpCompress.Common.Options;

/// <summary>
/// Options for controlling whether SharpCompress may use multi-threaded processing.
/// </summary>
public interface IParallelismOptions
{
    /// <summary>
    /// When true, opts in to any optional parallel (multi-threaded) processing a compressor may
    /// offer. Compressors that do not implement parallel processing ignore this setting. Default
    /// is false (single-threaded processing), so behavior is unchanged unless explicitly enabled.
    /// </summary>
    bool EnableParallelism { get; set; }
}
