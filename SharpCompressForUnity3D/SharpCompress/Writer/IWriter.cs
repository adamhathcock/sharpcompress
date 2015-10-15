namespace SharpCompress.Writer
{
    using SharpCompress.Common;
    using System;
    using System.IO;

    public interface IWriter : IDisposable
    {
        void Write(string filename, Stream source, DateTime? modificationTime);

        ArchiveType WriterType { get; }
    }
}

