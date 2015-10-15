namespace SharpCompress.Reader
{
    using SharpCompress.Common;
    using System;

    internal interface IReaderExtractionListener : IExtractionListener
    {
        void FireEntryExtractionBegin(Entry entry);
        void FireEntryExtractionEnd(Entry entry);
    }
}

