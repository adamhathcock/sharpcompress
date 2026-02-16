using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using SharpCompress.Common;

namespace SharpCompress.Compressors.Arj;

public partial class BitReader
{
    /// <summary>
    /// Asynchronously reads a single bit from the stream. Returns 0 or 1.
    /// </summary>
    public async ValueTask<int> ReadBitAsync(CancellationToken cancellationToken)
    {
        if (_bitCount == 0)
        {
            var buffer = new byte[1];
            int bytesRead = await _input
                .ReadAsync(buffer, 0, 1, cancellationToken)
                .ConfigureAwait(false);
            if (bytesRead < 1)
            {
                throw new IncompleteArchiveException("No more data available in BitReader.");
            }

            _bitBuffer = buffer[0];
            _bitCount = 8;
        }

        int bit = (_bitBuffer >> (_bitCount - 1)) & 1;
        _bitCount--;
        return bit;
    }

    /// <summary>
    /// Asynchronously reads n bits (up to 32) from the stream.
    /// </summary>
    public async ValueTask<int> ReadBitsAsync(int count, CancellationToken cancellationToken)
    {
        if (count < 0 || count > 32)
        {
            throw new ArgumentOutOfRangeException(nameof(count), "Count must be between 0 and 32.");
        }

        int result = 0;
        for (int i = 0; i < count; i++)
        {
            result = (result << 1) | await ReadBitAsync(cancellationToken).ConfigureAwait(false);
        }
        return result;
    }
}
