using SharpCompress.Common;

namespace SharpCompress.Reader
{
    internal interface IReaderExtractionListener : IExtractionListener
    {
        //        void EnsureEntriesLoaded();
        void FireEntryExtractionBegin(Entry entry);
        void FireEntryExtractionEnd(Entry entry);
    }
}