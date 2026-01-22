using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using SharpCompress.Common;

namespace SharpCompress.Writers;

public abstract partial class AbstractWriter
{
    public virtual async ValueTask WriteAsync(
        string filename,
        Stream source,
        DateTime? modificationTime,
        CancellationToken cancellationToken = default
    )
    {
        // Default implementation calls synchronous version
        // Derived classes should override for true async behavior
        Write(filename, source, modificationTime);
        await Task.CompletedTask.ConfigureAwait(false);
    }

    public virtual async ValueTask WriteDirectoryAsync(
        string directoryName,
        DateTime? modificationTime,
        CancellationToken cancellationToken = default
    )
    {
        // Default implementation calls synchronous version
        // Derived classes should override for true async behavior
        WriteDirectory(directoryName, modificationTime);
        await Task.CompletedTask.ConfigureAwait(false);
    }

    public ValueTask DisposeAsync()
    {
        if (!_isDisposed)
        {
            GC.SuppressFinalize(this);
            Dispose(true);
            _isDisposed = true;
        }

        return new();
    }
}
