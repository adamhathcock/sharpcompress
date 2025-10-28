using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using SharpCompress.Common;

namespace SharpCompress.Writers;

#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.
public abstract class AbstractWriter(ArchiveType type, WriterOptions writerOptions) : IWriter
{
    private bool _isDisposed;

    //always initializes the stream

    protected void InitializeStream(Stream stream) => OutputStream = stream;

    protected Stream OutputStream { get; private set; }
#pragma warning restore CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.

    public ArchiveType WriterType { get; } = type;

    protected WriterOptions WriterOptions { get; } = writerOptions;

    public abstract void Write(string filename, Stream source, DateTime? modificationTime);

    public virtual async Task WriteAsync(
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

    public abstract void WriteDirectory(string directoryName, DateTime? modificationTime);

    public virtual async Task WriteDirectoryAsync(
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

    protected virtual void Dispose(bool isDisposing)
    {
        if (isDisposing)
        {
            OutputStream.Dispose();
        }
    }

    public void Dispose()
    {
        if (!_isDisposed)
        {
            GC.SuppressFinalize(this);
            Dispose(true);
            _isDisposed = true;
        }
    }

    ~AbstractWriter()
    {
        if (!_isDisposed)
        {
            Dispose(false);
            _isDisposed = true;
        }
    }
}
