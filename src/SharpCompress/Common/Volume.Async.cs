using System;
using System.Threading.Tasks;

namespace SharpCompress.Common;

public abstract partial class Volume
{
    public virtual async ValueTask DisposeAsync()
    {
#if LEGACY_DOTNET
        _actualStream.Dispose();
        await Task.CompletedTask.ConfigureAwait(false);
#else
        await _actualStream.DisposeAsync().ConfigureAwait(false);
#endif
        GC.SuppressFinalize(this);
    }
}
