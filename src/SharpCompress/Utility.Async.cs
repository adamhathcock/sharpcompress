using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace SharpCompress;

internal static partial class Utility
{
    /// <summary>
    /// Read exactly the requested number of bytes from a stream asynchronously. Throws EndOfStreamException if not enough data is available.
    /// </summary>
    public static async ValueTask ReadExactAsync(
        this Stream stream,
        byte[] buffer,
        int offset,
        int length,
        CancellationToken cancellationToken = default
    )
    {
        if (stream is null)
        {
            throw new ArgumentNullException(nameof(stream));
        }

        if (buffer is null)
        {
            throw new ArgumentNullException(nameof(buffer));
        }

        if (offset < 0 || offset > buffer.Length)
        {
            throw new ArgumentOutOfRangeException(nameof(offset));
        }

        if (length < 0 || length > buffer.Length - offset)
        {
            throw new ArgumentOutOfRangeException(nameof(length));
        }

        while (length > 0)
        {
            var fetched = await stream
                .ReadAsync(buffer, offset, length, cancellationToken)
                .ConfigureAwait(false);
            if (fetched <= 0)
            {
                throw new EndOfStreamException();
            }

            offset += fetched;
            length -= fetched;
        }
    }
}
