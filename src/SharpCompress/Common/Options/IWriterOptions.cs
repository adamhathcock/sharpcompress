using SharpCompress.Common;

namespace SharpCompress.Common.Options;

public interface IWriterOptions : IStreamOptions, IEncodingOptions, IProgressOptions
{
    CompressionType CompressionType { get; init; }
    int CompressionLevel { get; init; }
}
