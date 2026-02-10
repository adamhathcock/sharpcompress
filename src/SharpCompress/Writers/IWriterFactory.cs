using System.IO;
using System.Threading;
using SharpCompress.Common.Options;
using SharpCompress.Factories;

namespace SharpCompress.Writers;

public interface IWriterFactory : IFactory
{
    IWriter OpenWriter(Stream stream, IWriterOptions writerOptions);

    IAsyncWriter OpenAsyncWriter(
        Stream stream,
        IWriterOptions writerOptions,
        CancellationToken cancellationToken = default
    );
}
