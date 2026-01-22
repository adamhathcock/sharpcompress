using System;
using System.Threading.Tasks;

namespace SharpCompress.Common;

public abstract partial class Volume
{
    public virtual async ValueTask DisposeAsync()
    {
#if NETFRAMEWORK || NETSTANDARD2_0
        await Task.Run(() => _actualStream.Dispose()).ConfigureAwait(false);
#else
        await _actualStream.DisposeAsync().ConfigureAwait(false);
#endif
        GC.SuppressFinalize(this);
    }
}
