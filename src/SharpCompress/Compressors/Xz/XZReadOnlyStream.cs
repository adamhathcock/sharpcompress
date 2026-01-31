using System.IO;
using SharpCompress.Common;
using SharpCompress.IO;

namespace SharpCompress.Compressors.Xz;

public abstract class XZReadOnlyStream : ReadOnlyStream, IStreamStack
{
    Stream IStreamStack.BaseStream() => base.BaseStream;

    public XZReadOnlyStream(Stream stream)
    {
        BaseStream = stream;
        if (!BaseStream.CanRead)
        {
            throw new InvalidFormatException("Must be able to read from stream");
        }
#if DEBUG_STREAMS
        this.DebugConstruct(typeof(XZReadOnlyStream));
#endif
    }

    protected override void Dispose(bool disposing)
    {
#if DEBUG_STREAMS
        this.DebugDispose(typeof(XZReadOnlyStream));
#endif
        base.Dispose(disposing);
    }
}
