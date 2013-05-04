using Windows.Foundation;
using Windows.Storage.Streams;

namespace SharpCompress
{
    public interface IReaderFactory
    {
        IAsyncOperation<IReader> Open(IInputStream stream);
    }
}