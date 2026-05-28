using System;
using System.Buffers.Binary;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using SharpCompress.Common;

namespace SharpCompress.Compressors.Xz;

public static partial class BinaryUtils
{
    public static async ValueTask<int> ReadLittleEndianInt32Async(
        this Stream stream,
        CancellationToken cancellationToken = default
    )
    {
        var bytes = new byte[4];
        var read = await stream.ReadFullyAsync(bytes, cancellationToken).ConfigureAwait(false);
        if (!read)
        {
            throw new IncompleteArchiveException("Unexpected end of stream.");
        }
        return BinaryPrimitives.ReadInt32LittleEndian(bytes);
    }

    internal static async ValueTask<uint> ReadLittleEndianUInt32Async(
        this Stream stream,
        CancellationToken cancellationToken = default
    ) =>
        unchecked(
            (uint)await ReadLittleEndianInt32Async(stream, cancellationToken).ConfigureAwait(false)
        );
}
