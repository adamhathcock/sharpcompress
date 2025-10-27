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
    Task WriteAsync(
        string filename,
        Stream source,
        DateTime? modificationTime,
        CancellationToken cancellationToken = default
    );
}
