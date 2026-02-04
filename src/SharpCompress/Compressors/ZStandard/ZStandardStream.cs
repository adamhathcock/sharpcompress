using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace SharpCompress.Compressors.ZStandard;

internal partial class ZStandardStream : DecompressionStream
{
    private readonly Stream stream;

    internal static bool IsZStandard(Stream stream)
    {
        var buffer = new byte[4];
        var bytesRead = stream.Read(buffer, 0, 4);
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
