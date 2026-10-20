using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using SharpCompress.Compressors.RLE90;
using SharpCompress.IO;

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
        cancellationToken.ThrowIfCancellationRequested();
        if (count == 0)
        {
            return 0;
        }
        if (!_processed)
        {
            using var limited = new ReadOnlySubStream(_stream, _compressedSize);
            using var data = new PooledMemoryStream();
            await limited.CopyToAsync(data, 81920, cancellationToken).ConfigureAwait(false);
            FinishDecompression(data);
        }
        return Read(buffer, offset, count);
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
