namespace SharpCompress.Common
{
    using System;
    using System.IO;

    public abstract class FilePart
    {
        protected FilePart()
        {
        }

        internal abstract Stream GetCompressedStream();
        internal abstract Stream GetRawStream();

        internal abstract string FilePartName { get; }
    }
}

