namespace SharpCompress.Archive
{
    using SharpCompress.Common;
    using System;
    using System.IO;
    using System.Runtime.InteropServices;

    public interface IWritableArchive : IArchive, IDisposable
    {
        IArchiveEntry AddEntry(string key, Stream source, bool closeStream,  long size,  DateTime? modified);
        void RemoveEntry(IArchiveEntry entry);
        void SaveTo(Stream stream, CompressionInfo compressionType);
    }
}

