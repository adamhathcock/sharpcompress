namespace SharpCompress.Archive
{
    using SharpCompress.Common;
    using SharpCompress.Reader;
    using System;
    using System.Collections.Generic;

    public interface IArchive : IDisposable
    {
        event EventHandler<CompressedBytesReadEventArgs> CompressedBytesRead;

        event EventHandler<ArchiveExtractionEventArgs<IArchiveEntry>> EntryExtractionBegin;

        event EventHandler<ArchiveExtractionEventArgs<IArchiveEntry>> EntryExtractionEnd;

        event EventHandler<FilePartExtractionBeginEventArgs> FilePartExtractionBegin;

        IReader ExtractAllEntries();

        IEnumerable<IArchiveEntry> Entries { get; }

        bool IsComplete { get; }

        bool IsSolid { get; }

        long TotalSize { get; }

        long TotalUncompressSize { get; }

        ArchiveType Type { get; }

        IEnumerable<IVolume> Volumes { get; }
    }
}

