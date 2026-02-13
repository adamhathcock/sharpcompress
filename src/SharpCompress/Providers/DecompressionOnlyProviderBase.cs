using System;
using System.IO;
using SharpCompress.Common;

namespace SharpCompress.Providers;

public abstract class DecompressionOnlyProviderBase : CompressionProviderBase
{
    public override bool SupportsCompression => false;
    public override bool SupportsDecompression => true;

    protected abstract string CompressionNotSupportedMessage { get; }

    public sealed override Stream CreateCompressStream(Stream destination, int compressionLevel) =>
        throw new NotSupportedException(CompressionNotSupportedMessage);

    public sealed override Stream CreateCompressStream(
        Stream destination,
        int compressionLevel,
        CompressionContext context
    ) => throw new NotSupportedException(CompressionNotSupportedMessage);
}
