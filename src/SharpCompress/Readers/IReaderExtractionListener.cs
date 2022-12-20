using SharpCompress.Common;

namespace SharpCompress.Readers;

public interface IReaderExtractionListener : IExtractionListener
{
    void FireEntryExtractionProgress(Entry entry, long sizeTransferred, int iterations);
}
