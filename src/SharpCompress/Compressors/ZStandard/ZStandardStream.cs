using System;
using System.IO;
using System.Text;
using SharpCompress.IO;

namespace SharpCompress.Compressors.ZStandard;

internal class ZStandardStream : ZstdSharp.DecompressionStream, IStreamStack
{
#if DEBUG_STREAMS
    long IStreamStack.InstanceId { get; set; }
#endif

    public int DefaultBufferSize { get; set; }
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

    private readonly Stream stream;

    Stream IStreamStack.BaseStream() => stream;

    void IStreamStack.SetPosition(long position) { }

    internal static bool IsZStandard(Stream stream)
    {
        using var br = new BinaryReader(stream,  Encoding.UTF8, true);
        var magic = br.ReadUInt32();
        if (ZstandardConstants.MAGIC != magic)
        {
            return false;
        }
        return true;
    }

    public ZStandardStream(Stream baseInputStream)
        : base(baseInputStream)
    {
        this.stream = baseInputStream;
#if DEBUG_STREAMS
        this.DebugConstruct(typeof(ZStandardStream));
#endif
    }

    /// <summary>
    /// The current position within the stream.
    /// Throws a NotSupportedException when attempting to set the position
    /// </summary>
    /// <exception cref="NotSupportedException">Attempting to set the position</exception>
    public override long Position
    {
        get => stream.Position;
        set => throw new NotSupportedException("InflaterInputStream Position not supported");
    }
}
