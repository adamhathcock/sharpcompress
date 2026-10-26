using System;
using System.Runtime.ExceptionServices;
using System.Threading;
using System.Threading.Tasks;

namespace SharpCompress.Compressors.Xz.Filters;

public abstract partial class BlockFilter
{
    internal Task<int> ReadBaseAsync(
        byte[] buffer,
        int offset,
        int count,
        CancellationToken cancellationToken
    )
    {
        try
        {
            // A single adapter belongs at the public Task boundary, not at every wrapper.
            if (BaseStream is BcjFilterStream filter)
            {
                // Buffered reads need no async state machine, only the public result task.
                return filter.TryReadBuffered(
                    buffer,
                    offset,
                    count,
                    cancellationToken,
                    out var read
                )
                    ? Task.FromResult(read)
                    : filter.ReadCoreAsync(buffer, offset, count, cancellationToken).AsTask();
            }
            return BaseStream.ReadAsync(buffer, offset, count, cancellationToken);
        }
        catch (Exception exception)
        {
            // A custom subclass may install a stream that throws before returning a task.
            // Preserve the old async method's faulted/canceled-task semantics in that case.
            return RethrowReadExceptionAsync(exception);
        }
    }

    private static async Task<int> RethrowReadExceptionAsync(Exception exception)
    {
        await Task.CompletedTask.ConfigureAwait(false);
        ExceptionDispatchInfo.Capture(exception).Throw();
        return 0;
    }
}
