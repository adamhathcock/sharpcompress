using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using SharpCompress.Common;

namespace SharpCompress.Writers
{
    public interface IWriter : IAsyncDisposable
    {
        ArchiveType WriterType { get; }
        Task WriteAsync(string filename, Stream source, DateTime? modificationTime, CancellationToken cancellationToken);
    }
}