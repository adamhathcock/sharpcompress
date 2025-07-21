using System.IO;
using SharpCompress.Common;
using SharpCompress.IO;

namespace SharpCompress.Compressors.Xz;

public abstract class XZReadOnlyStream : ReadOnlyStream, IStreamStack
{
#if DEBUG_STREAMS
    long IStreamStack.InstanceId { get; set; }
#endif
    int IStreamStack.DefaultBufferSize { get; set; }

    Stream IStreamStack.BaseStream() => base.BaseStream;

    int IStreamStack.BufferSize
    {
        get => 0;
        set { }
    }
    int IStreamStack.BufferPosition
    {
        get => 0;
        set { }
    }

    void IStreamStack.SetPosition(long position) { }

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
