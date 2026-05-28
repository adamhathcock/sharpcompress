using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using SharpCompress.Compressors.Filters;

namespace SharpCompress.Compressors.Xz.Filters;

public partial class X86Filter
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
        BranchExecFilter.X86Converter(buffer, _ip, ref _state);
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
        // X86Converter requires byte[], so we need to copy to an array
        var arrayBuffer = buffer.Slice(0, bytesRead).ToArray();
        BranchExecFilter.X86Converter(arrayBuffer, _ip, ref _state);
        arrayBuffer.AsSpan(0, bytesRead).CopyTo(buffer.Span);
        _ip += (uint)bytesRead;
        return bytesRead;
    }
#endif
}
