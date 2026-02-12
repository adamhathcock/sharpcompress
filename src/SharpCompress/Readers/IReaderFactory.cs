using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace SharpCompress.Readers;

public interface IReaderFactory : Factories.IFactory
{
    /// <summary>
    /// Opens a Reader for Non-seeking usage.
    /// </summary>
    /// <param name="stream">An open, readable stream.</param>
    /// <param name="options">Reader options.</param>
    /// <returns>The opened reader.</returns>
    IReader OpenReader(Stream stream, ReaderOptions? options);

    /// <summary>
    /// Opens a Reader for Non-seeking usage asynchronously.
    /// </summary>
    /// <param name="stream">An open, readable stream.</param>
    /// <param name="options">Reader options.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A <see cref="ValueTask{TResult}"/> containing the opened async reader.</returns>
    ValueTask<IAsyncReader> OpenAsyncReader(
        Stream stream,
        ReaderOptions? options,
        CancellationToken cancellationToken
    );
}
