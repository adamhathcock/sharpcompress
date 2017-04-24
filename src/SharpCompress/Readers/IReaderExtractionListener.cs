using SharpCompress.Common;

namespace SharpCompress.Readers
{
    internal interface IReaderExtractionListener : IExtractionListener
    {
        //        void EnsureEntriesLoaded();
        void FireEntryExtractionBegin(Entry entry);
        void FireEntryExtractionEnd(Entry entry);
        void FireEntryExtractionProgress(Entry entry, long sizeTransferred, int iterations);
    }
}