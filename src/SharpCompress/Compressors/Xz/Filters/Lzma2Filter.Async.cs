using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace SharpCompress.Compressors.Xz.Filters;

public partial class Lzma2Filter
{
    public override Task<int> ReadAsync(
        byte[] buffer,
        int offset,
        int count,
        CancellationToken cancellationToken
    ) => BaseStream.ReadAsync(buffer, offset, count, cancellationToken);

#if !LEGACY_DOTNET
    public override ValueTask<int> ReadAsync(
        Memory<byte> buffer,
        CancellationToken cancellationToken = default
    ) => BaseStream.ReadAsync(buffer, cancellationToken);
#endif
}
