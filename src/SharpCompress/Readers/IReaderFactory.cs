using System.IO;
using System.Threading;
using System.Threading.Tasks;

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

    /// <summary>
    /// Opens a Reader asynchronously for Non-seeking usage
    /// </summary>
    /// <param name="stream"></param>
    /// <param name="options"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    ValueTask<IReader> OpenReaderAsync(
        Stream stream,
        ReaderOptions? options,
        CancellationToken cancellationToken = default
    );
}
