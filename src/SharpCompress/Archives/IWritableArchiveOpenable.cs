#if NET8_0_OR_GREATER
namespace SharpCompress.Archives;

public interface IWritableArchiveOpenable
    : IArchiveOpenable<IWritableArchive, IWritableAsyncArchive>
{
    public static abstract IWritableArchive CreateArchive();
    public static abstract IWritableAsyncArchive CreateAsyncArchive();
}
#endif