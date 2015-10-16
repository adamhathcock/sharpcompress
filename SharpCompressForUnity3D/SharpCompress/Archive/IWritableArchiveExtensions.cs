namespace SharpCompress.Archive
{
    using SharpCompress.Common;
    using System;
    using System.IO;

    public static class IWritableArchiveExtensions
    {
        public static void SaveTo(IWritableArchive writableArchive, Stream stream, CompressionType compressionType)
        {
            CompressionInfo info = new CompressionInfo();
            info.Type = compressionType;
            writableArchive.SaveTo(stream, info);
        }
    }
}

