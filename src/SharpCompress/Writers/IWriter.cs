using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using SharpCompress.Common;

namespace SharpCompress.Writers;

public interface IWriter : IDisposable
{
    ArchiveType WriterType { get; }
    void Write(string filename, Stream source, DateTime? modificationTime);
    void WriteDirectory(string directoryName, DateTime? modificationTime);
}

public interface IAsyncWriter : IDisposable
{
    ArchiveType WriterType { get; }
    ValueTask WriteAsync(
        string filename,
        Stream source,
        DateTime? modificationTime,
        CancellationToken cancellationToken = default
    );
    ValueTask WriteDirectoryAsync(
        string directoryName,
        DateTime? modificationTime,
        CancellationToken cancellationToken = default
    );
}
