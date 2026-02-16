using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace SharpCompress.Compressors.RLE90;

public partial class RunLength90Stream
{
    public override async Task<int> ReadAsync(
        byte[] buffer,
        int offset,
        int count,
        CancellationToken cancellationToken
    )
    {
        ThrowHelper.ThrowIfNull(buffer);

        if (offset < 0 || count < 0 || offset + count > buffer.Length)
        {
            throw new ArgumentOutOfRangeException(nameof(offset));
        }

        int bytesWritten = 0;

        while (bytesWritten < count && !_endOfCompressedData)
        {
            // Handle pending repeat bytes first
            if (_repeatCount > 0)
            {
                int toWrite = Math.Min(_repeatCount, count - bytesWritten);
                for (int i = 0; i < toWrite; i++)
                {
                    buffer[offset + bytesWritten++] = _lastByte;
                }
                _repeatCount -= toWrite;
                continue;
            }

            // Try to read the next byte from compressed data
            if (_bytesReadFromSource >= _compressedSize)
            {
                _endOfCompressedData = true;
                break;
            }

            byte[] singleByte = new byte[1];
            int bytesRead = await _stream
                .ReadAsync(singleByte, 0, 1, cancellationToken)
                .ConfigureAwait(false);
            if (bytesRead == 0)
            {
                _endOfCompressedData = true;
                break;
            }

            _bytesReadFromSource++;
            byte c = singleByte[0];

            if (_inDleMode)
            {
                _inDleMode = false;

                if (c == 0)
                {
                    buffer[offset + bytesWritten++] = DLE;
                    _lastByte = DLE;
                }
                else
                {
                    _repeatCount = c - 1;
                    // We'll handle these repeats in next loop iteration.
                }
            }
            else if (c == DLE)
            {
                _inDleMode = true;
            }
            else
            {
                buffer[offset + bytesWritten++] = c;
                _lastByte = c;
            }
        }

        return bytesWritten;
    }

#if !LEGACY_DOTNET
    public override async ValueTask<int> ReadAsync(
        Memory<byte> buffer,
        CancellationToken cancellationToken = default
    )
    {
        if (buffer.IsEmpty)
        {
            return 0;
        }

        byte[] array = System.Buffers.ArrayPool<byte>.Shared.Rent(buffer.Length);
        try
        {
            int read = await ReadAsync(array, 0, buffer.Length, cancellationToken)
                .ConfigureAwait(false);
            array.AsSpan(0, read).CopyTo(buffer.Span);
            return read;
        }
        finally
        {
            System.Buffers.ArrayPool<byte>.Shared.Return(array);
        }
    }
#endif
}
