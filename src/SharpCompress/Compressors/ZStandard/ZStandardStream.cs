using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using SharpCompress.IO;

namespace SharpCompress.Compressors.ZStandard;

internal class ZStandardStream : DecompressionStream, IStreamStack
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
        var br = new BinaryReader(stream);
        var magic = br.ReadUInt32();
        if (ZstandardConstants.MAGIC != magic)
        {
            return false;
        }
        return true;
    }

    internal static async ValueTask<bool> IsZStandardAsync(
        Stream stream,
        CancellationToken cancellationToken = default
    )
    {
        cancellationToken.ThrowIfCancellationRequested();
        var buffer = new byte[4];
        var bytesRead = await stream.ReadAsync(buffer, 0, 4, cancellationToken);
        if (bytesRead < 4)
        {
            return false;
        }

        var magic = BitConverter.ToUInt32(buffer, 0);
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
        get { return stream.Position; }
        set { throw new NotSupportedException("InflaterInputStream Position not supported"); }
    }
}
