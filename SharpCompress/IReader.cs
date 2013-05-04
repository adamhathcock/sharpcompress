using Windows.Storage.Streams;

namespace SharpCompress
{
    public interface IReader
    {
        ArchiveType ArchiveType
        {
            get;
        }

        IEntry Entry
        {
            get;
        }
        void WriteEntryTo(IOutputStream writableStream);
        bool MoveToNextEntry();
    }
}