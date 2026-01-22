using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using SharpCompress.IO;

namespace SharpCompress.Compressors.BZip2;

public sealed partial class BZip2Stream
{
#if !LEGACY_DOTNET
    public override async ValueTask<int> ReadAsync(
        Memory<byte> buffer,
        CancellationToken cancellationToken = default
    ) => await stream.ReadAsync(buffer, cancellationToken).ConfigureAwait(false);

    public override async ValueTask WriteAsync(
        ReadOnlyMemory<byte> buffer,
        CancellationToken cancellationToken = default
    ) => await stream.WriteAsync(buffer, cancellationToken).ConfigureAwait(false);
#endif

    public override async Task<int> ReadAsync(
        byte[] buffer,
        int offset,
        int count,
        CancellationToken cancellationToken = default
    ) => await stream.ReadAsync(buffer, offset, count, cancellationToken).ConfigureAwait(false);

    public override async Task WriteAsync(
        byte[] buffer,
        int offset,
        int count,
        CancellationToken cancellationToken = default
    ) => await stream.WriteAsync(buffer, offset, count, cancellationToken).ConfigureAwait(false);
}
