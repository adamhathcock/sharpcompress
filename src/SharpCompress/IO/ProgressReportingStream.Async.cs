using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace SharpCompress.IO;

internal sealed partial class ProgressReportingStream
{
    public override async Task<int> ReadAsync(
        byte[] buffer,
        int offset,
        int count,
        CancellationToken cancellationToken
    )
    {
        var bytesRead = await _baseStream
            .ReadAsync(buffer, offset, count, cancellationToken)
            .ConfigureAwait(false);
        if (bytesRead > 0)
        {
            _bytesTransferred += bytesRead;
            ReportProgress();
        }
        return bytesRead;
    }

#if !LEGACY_DOTNET
    public override async ValueTask<int> ReadAsync(
        Memory<byte> buffer,
        CancellationToken cancellationToken = default
    )
    {
        var bytesRead = await _baseStream
            .ReadAsync(buffer, cancellationToken)
            .ConfigureAwait(false);
        if (bytesRead > 0)
        {
            _bytesTransferred += bytesRead;
            ReportProgress();
        }
        return bytesRead;
    }
#endif

#if !LEGACY_DOTNET
    public override async ValueTask DisposeAsync()
    {
        if (!_leaveOpen)
        {
            await _baseStream.DisposeAsync().ConfigureAwait(false);
        }
        await base.DisposeAsync().ConfigureAwait(false);
    }
#endif
}
