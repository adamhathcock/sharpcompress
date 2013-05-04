using System.IO;
using System.Threading.Tasks;
using Windows.Storage.Streams;

namespace SharpCompress
{
    internal class WrappedReader : IReader
    {
        private readonly Reader.IReader reader;

        internal WrappedReader(Reader.IReader reader)
        {
            this.reader = reader;
        }

        public ArchiveType ArchiveType
        {
            get { return (ArchiveType)reader.ArchiveType; }
        }

        public IEntry Entry
        {
            get;
            private set;
        }

        public async void WriteEntryTo(IOutputStream writableStream)
        {
            await Task.Run(() => reader.WriteEntryTo(writableStream.AsStreamForWrite()));
        }

        public bool MoveToNextEntry()
        {
            if (reader.MoveToNextEntry())
            {
                Entry = new WrappedEntry(reader.Entry);
                return true;
            }
            Entry = null;
            return false;
        }
    }
}