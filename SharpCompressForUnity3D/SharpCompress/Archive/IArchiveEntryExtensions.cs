namespace SharpCompress.Archive
{
    using SharpCompress;
    using SharpCompress.Common;
    using SharpCompress.IO;
    using System;
    using System.IO;
    using System.Runtime.CompilerServices;

    //[Extension]
    public static class IArchiveEntryExtensions
    {
        //[Extension]
        public static void WriteTo( IArchiveEntry archiveEntry, Stream streamToWriteTo)
        {
            if ((archiveEntry.Archive.Type == ArchiveType.Rar) && archiveEntry.Archive.IsSolid)
            {
                throw new InvalidFormatException("Cannot use Archive random access on SOLID Rar files.");
            }
            if (archiveEntry.IsDirectory)
            {
                throw new ExtractionException("Entry is a file directory and cannot be extracted.");
            }
            IArchiveExtractionListener archive = archiveEntry.Archive as IArchiveExtractionListener;
            archive.EnsureEntriesLoaded();
            archive.FireEntryExtractionBegin(archiveEntry);
            archive.FireFilePartExtractionBegin(archiveEntry.Key, archiveEntry.Size, archiveEntry.CompressedSize);
            Stream stream = archiveEntry.OpenEntryStream();
            if (stream != null)
            {
                using (stream)
                {
                    using (Stream stream2 = new ListeningStream(archive, stream))
                    {
                        Utility.TransferTo(stream2, streamToWriteTo);
                    }
                }
                archive.FireEntryExtractionEnd(archiveEntry);
            }
        }
    }
}

