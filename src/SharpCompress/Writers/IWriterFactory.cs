using System.IO;
using System.Threading;
using System.Threading.Tasks;
using SharpCompress.Common.Options;
using SharpCompress.Factories;

namespace SharpCompress.Writers;

public interface IWriterFactory : IFactory
{
    IWriter OpenWriter(Stream stream, IWriterOptions writerOptions);

    ValueTask<IAsyncWriter> OpenAsyncWriter(
        Stream stream,
        IWriterOptions writerOptions,
        CancellationToken cancellationToken = default
    );
}
