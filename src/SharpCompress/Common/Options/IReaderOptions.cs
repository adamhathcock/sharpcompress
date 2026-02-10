namespace SharpCompress.Common.Options;

public interface IReaderOptions
    : IStreamOptions,
        IEncodingOptions,
        IProgressOptions,
        IExtractionOptions
{
    bool LookForHeader { get; init; }
    string? Password { get; init; }
    bool DisableCheckIncomplete { get; init; }
    int BufferSize { get; init; }
    string? ExtensionHint { get; init; }
    int? RewindableBufferSize { get; init; }
}
