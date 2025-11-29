namespace SharpCompress.Archives;

internal interface IArchiveExtractionListener
{
    void EnsureEntriesLoaded();
    void FireEntryExtractionBegin(IArchiveEntry entry);
    void FireEntryExtractionEnd(IArchiveEntry entry);
}
