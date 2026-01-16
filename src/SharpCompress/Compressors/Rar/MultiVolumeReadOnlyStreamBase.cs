using System.IO;

namespace SharpCompress.Compressors.Rar;

internal abstract class MultiVolumeReadOnlyStreamBase : Stream
{
    public byte[]? CurrentCrc { get; protected set; }
}
