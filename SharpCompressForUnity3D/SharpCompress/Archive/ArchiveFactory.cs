namespace SharpCompress.Archive
{
    using SharpCompress;
    using SharpCompress.Archive.GZip;
    using SharpCompress.Archive.Rar;
    using SharpCompress.Archive.SevenZip;
    using SharpCompress.Archive.Tar;
    using SharpCompress.Archive.Zip;
    using SharpCompress.Common;
    using System;
    using System.IO;
    using System.Runtime.InteropServices;

    public class ArchiveFactory
    {
        public static IWritableArchive Create(ArchiveType type)
        {
            switch (type)
            {
                case ArchiveType.Zip:
                    return ZipArchive.Create();

                case ArchiveType.Tar:
                    return TarArchive.Create();

                case ArchiveType.GZip:
                    return GZipArchive.Create();
            }
            throw new NotSupportedException("Cannot create Archives of type: " + type);
        }
        public static IArchive Open(Stream stream) {
            return Open(stream, Options.KeepStreamsOpen);
        }
        public static IArchive Open(Stream stream,  Options options)
        {
            Utility.CheckNotNull(stream, "stream");
            if (!(stream.CanRead && stream.CanSeek))
            {
                throw new ArgumentException("Stream should be readable and seekable");
            }
            if (ZipArchive.IsZipFile(stream, null))
            {
                stream.Seek(0L, SeekOrigin.Begin);
                return ZipArchive.Open(stream, options, null);
            }
            stream.Seek(0L, SeekOrigin.Begin);
            if (TarArchive.IsTarFile(stream))
            {
                stream.Seek(0L, SeekOrigin.Begin);
                return TarArchive.Open(stream, options);
            }
            stream.Seek(0L, SeekOrigin.Begin);
            if (SevenZipArchive.IsSevenZipFile(stream))
            {
                stream.Seek(0L, SeekOrigin.Begin);
                return SevenZipArchive.Open(stream, options);
            }
            stream.Seek(0L, SeekOrigin.Begin);
            if (GZipArchive.IsGZipFile(stream))
            {
                stream.Seek(0L, SeekOrigin.Begin);
                return GZipArchive.Open(stream, options);
            }
            stream.Seek(0L, SeekOrigin.Begin);
            if (!RarArchive.IsRarFile(stream, Options.LookForHeader | Options.KeepStreamsOpen))
            {
                throw new InvalidOperationException("Cannot determine compressed stream type. Supported Archive Formats: Zip, GZip, Tar, Rar, 7Zip");
            }
            stream.Seek(0L, SeekOrigin.Begin);
            return RarArchive.Open(stream, options, null);
        }
    }
}

