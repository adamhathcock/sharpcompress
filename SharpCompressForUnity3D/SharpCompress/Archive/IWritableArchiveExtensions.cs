namespace SharpCompress.Archive
{
    using SharpCompress.Common;
    using System;
    using System.IO;
    using System.Runtime.CompilerServices;

    //[Extension]
    public static class IWritableArchiveExtensions
    {
        //[Extension]
        public static void SaveTo( IWritableArchive writableArchive, Stream stream, CompressionType compressionType)
        {
            CompressionInfo info = new CompressionInfo();
            info.Type = compressionType;
            writableArchive.SaveTo(stream, info);
        }
    }
}

