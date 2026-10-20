using System;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;

namespace SharpCompress.IO;

internal readonly struct ConfiguredAsyncDisposeScope(
    IDisposable? resource,
    bool continueOnCapturedContext
)
{
    public ConfiguredValueTaskAwaitable DisposeAsync()
    {
        if (resource is IAsyncDisposable asyncDisposable)
        {
            return asyncDisposable.DisposeAsync().ConfigureAwait(continueOnCapturedContext);
        }

        resource?.Dispose();
        return default(ValueTask).ConfigureAwait(continueOnCapturedContext);
    }
}
