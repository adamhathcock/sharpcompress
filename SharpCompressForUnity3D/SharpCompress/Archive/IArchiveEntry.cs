namespace SharpCompress.Archive
{
    using SharpCompress.Common;
    using System;
    using System.IO;

    public interface IArchiveEntry : IEntry
    {
        Stream OpenEntryStream();

        IArchive Archive { get; }

        bool IsComplete { get; }
    }
}

