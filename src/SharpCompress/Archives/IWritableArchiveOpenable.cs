using System.Threading.Tasks;
using SharpCompress.Common.Options;

#if NET8_0_OR_GREATER
namespace SharpCompress.Archives;

public interface IWritableArchiveOpenable<TOptions>
    : IArchiveOpenable<IWritableArchive<TOptions>, IWritableAsyncArchive<TOptions>>
    where TOptions : IWriterOptions
{
    public static abstract IWritableArchive<TOptions> CreateArchive();
    public static abstract ValueTask<IWritableAsyncArchive<TOptions>> CreateAsyncArchive();
}
#endif
