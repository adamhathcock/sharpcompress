using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using SharpCompress.Compressors.Filters;

namespace SharpCompress.Compressors.Xz.Filters;

public partial class IA64Filter
{
    public override async Task<int> ReadAsync(
        byte[] buffer,
        int offset,
        int count,
        CancellationToken cancellationToken
    )
    {
        var bytesRead = await BaseStream
            .ReadAsync(buffer, offset, count, cancellationToken)
            .ConfigureAwait(false);
        BranchExecFilter.IA64Converter(buffer, _ip);
        _ip += (uint)bytesRead;
        return bytesRead;
    }

#if !LEGACY_DOTNET
    public override async ValueTask<int> ReadAsync(
        Memory<byte> buffer,
        CancellationToken cancellationToken = default
    )
    {
        var bytesRead = await BaseStream.ReadAsync(buffer, cancellationToken).ConfigureAwait(false);
        var arrayBuffer = buffer.Slice(0, bytesRead).ToArray();
        BranchExecFilter.IA64Converter(arrayBuffer, _ip);
        arrayBuffer.AsSpan(0, bytesRead).CopyTo(buffer.Span);
        _ip += (uint)bytesRead;
        return bytesRead;
    }
#endif
}
