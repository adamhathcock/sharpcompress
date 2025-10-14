using SharpCompress.Common;

namespace SharpCompress.Readers;

public interface IReaderExtractionListener : IExtractionListener
{
#pragma warning disable CA1030
  void FireEntryExtractionProgress(Entry entry, long sizeTransferred, int iterations);
#pragma warning restore CA1030
}
