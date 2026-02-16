using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using SharpCompress.Compressors.RLE90;

namespace SharpCompress.Compressors.ArcLzw;

public partial class ArcLzwStream
{
    public override async Task<int> ReadAsync(
        byte[] buffer,
        int offset,
        int count,
        CancellationToken cancellationToken
    )
    {
        if (_processed)
        {
            return 0;
        }
        _processed = true;
        var data = new byte[_compressedSize];
        int totalRead = 0;
        while (totalRead < _compressedSize)
        {
            int read = await _stream
                .ReadAsync(data, totalRead, _compressedSize - totalRead, cancellationToken)
                .ConfigureAwait(false);
            if (read == 0)
            {
                break;
            }
            totalRead += read;
        }
        var decoded = Decompress(data, _useCrunched);
        var result = decoded.Count;
        if (_useCrunched)
        {
            var unpacked = RLE.UnpackRLE(decoded.ToArray());
            unpacked.CopyTo(buffer, 0);
            result = unpacked.Count;
        }
        else
        {
            decoded.CopyTo(buffer, 0);
        }
        return result;
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
