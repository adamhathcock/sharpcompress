using System;
using System.IO;
using SharpCompress.Common;

namespace SharpCompress.Archive
{
    public interface IWritableArchive : IArchive
    {
        void RemoveEntry(IArchiveEntry entry);
        //IArchiveEntry AddEntry(string key, Stream source, bool closeStream);
        //IArchiveEntry AddEntry(string key, Stream source, bool closeStream,long size);
        IArchiveEntry AddEntry(string key, Stream source, bool closeStream, long size , DateTime? modified );

        void SaveTo(Stream stream, CompressionInfo compressionType);
    }
}