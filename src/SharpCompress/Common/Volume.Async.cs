using System;
using System.Threading.Tasks;

namespace SharpCompress.Common;

public abstract partial class Volume
{
    public virtual async ValueTask DisposeAsync()
    {
        await _actualStream.DisposeAsyncCompat().ConfigureAwait(false);
        GC.SuppressFinalize(this);
    }
}
