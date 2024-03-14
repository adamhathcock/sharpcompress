using SharpCompress.Common;

namespace SharpCompress.Writers;

public class WriterOptions : OptionsBase
{
    public WriterOptions(CompressionType compressionType) => CompressionType = compressionType;

    public CompressionType CompressionType { get; set; }

    public static implicit operator WriterOptions(CompressionType compressionType) =>
        new(compressionType);
}
