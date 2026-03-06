using System.Threading;
using System.Threading.Tasks;
using SharpCompress.Common.Options;

namespace SharpCompress.Archives;

/// <summary>
/// Decorator for <see cref="Factories.Factory"/> used to declare an archive format as able to create async writable archives.
/// </summary>
public interface IWritableAsyncArchiveFactory<TOptions> : Factories.IFactory
    where TOptions : IWriterOptions
{
    /// <summary>
    /// Creates a new, empty async archive, ready to be written.
    /// </summary>
    ValueTask<IWritableAsyncArchive<TOptions>> CreateAsyncArchive(
        CancellationToken cancellationToken = default
    );
}
