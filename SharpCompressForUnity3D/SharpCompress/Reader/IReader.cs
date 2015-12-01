namespace SharpCompress.Reader
{
    using SharpCompress.Common;
    using System;
    using System.IO;

    public interface IReader : IDisposable
    {
        event EventHandler<CompressedBytesReadEventArgs> CompressedBytesRead;

        event EventHandler<ReaderExtractionEventArgs<IEntry>> EntryExtractionBegin;

        event EventHandler<ReaderExtractionEventArgs<IEntry>> EntryExtractionEnd;

        event EventHandler<FilePartExtractionBeginEventArgs> FilePartExtractionBegin;

        void Cancel();
        bool MoveToNextEntry();
        EntryStream OpenEntryStream();
        void WriteEntryTo(Stream writableStream);

        SharpCompress.Common.ArchiveType ArchiveType { get; }

        bool Cancelled { get; }

        IEntry Entry { get; }
    }
}

