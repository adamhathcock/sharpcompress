using System.IO;
using System.Threading;
using SharpCompress.Factories;

namespace SharpCompress.Writers;

public interface IWriterFactory : IFactory
{
    IWriter Open(Stream stream, WriterOptions writerOptions);

    IWriter OpenAsync(
        Stream stream,
        WriterOptions writerOptions,
        CancellationToken cancellationToken = default
    );
}
