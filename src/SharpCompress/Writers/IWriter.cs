using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using SharpCompress.Common;

namespace SharpCompress.Writers;

public interface IWriter : IDisposable
#if !NETFRAMEWORK && !NETSTANDARD2_0
        , IAsyncDisposable
#endif
{
    ArchiveType WriterType { get; }
    void Write(string filename, Stream source, DateTime? modificationTime);
#if !NETFRAMEWORK && !NETSTANDARD2_0
    ValueTask WriteAsync(
        string filename,
        Stream source,
        DateTime? modificationTime,
        CancellationToken cancellationToken
    );
#endif
}
