using System.IO;
using SharpCompress.Common;

namespace SharpCompress.Compressors.Xz;

public abstract class XZReadOnlyStream : ReadOnlyStream
{
    public XZReadOnlyStream(Stream stream)
    {
        BaseStream = stream;
        if (!BaseStream.CanRead)
        {
            throw new InvalidFormatException("Must be able to read from stream");
        }
    }
}
