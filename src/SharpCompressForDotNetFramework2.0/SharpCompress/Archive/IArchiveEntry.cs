using System.IO;
using SharpCompress.Common;

namespace SharpCompress.Archive
{
    public interface IArchiveEntry : IEntry
    {
        /// <summary>
        /// Opens the current entry as a stream that will decompress as it is read.
        /// Read the entire stream or use SkipEntry on EntryStream.
        /// </summary>
        Stream OpenEntryStream();

        void WriteTo(Stream stream);

        /// <summary>
        /// The archive can find all the parts of the archive needed to extract this entry.
        /// </summary>
        bool IsComplete { get; }
    }
}
