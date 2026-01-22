using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using SharpCompress.Crypto;
using SharpCompress.IO;

namespace SharpCompress.Compressors.LZMA;

public sealed partial class LZipStream
{
#if !LEGACY_DOTNET
    /// <summary>
    /// Asynchronously reads bytes from the current stream into a buffer.
    /// </summary>
    public override ValueTask<int> ReadAsync(
        Memory<byte> buffer,
        CancellationToken cancellationToken = default
    ) => _stream.ReadAsync(buffer, cancellationToken);
#endif

    /// <summary>
    /// Asynchronously reads bytes from the current stream into a buffer.
    /// </summary>
    public override Task<int> ReadAsync(
        byte[] buffer,
        int offset,
        int count,
        CancellationToken cancellationToken = default
    ) => _stream.ReadAsync(buffer, offset, count, cancellationToken);

    /// <summary>
    /// Asynchronously writes bytes from a buffer to the current stream.
    /// </summary>
    public override async Task WriteAsync(
        byte[] buffer,
        int offset,
        int count,
        CancellationToken cancellationToken
    )
    {
        cancellationToken.ThrowIfCancellationRequested();
        await _stream.WriteAsync(buffer, offset, count, cancellationToken);
        _writeCount += count;
    }
}
