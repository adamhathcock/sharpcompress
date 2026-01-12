using System.IO;
using System.Threading;

namespace SharpCompress.Readers;

public interface IReaderFactory : Factories.IFactory
{
    /// <summary>
    /// Opens a Reader for Non-seeking usage
    /// </summary>
    /// <param name="stream"></param>
    /// <param name="options"></param>
    /// <returns></returns>
    IReader OpenReader(Stream stream, ReaderOptions? options);
    IAsyncReader OpenReaderAsync(
        Stream stream,
        ReaderOptions? options,
        CancellationToken cancellationToken
    );
}
