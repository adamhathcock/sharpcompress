namespace SharpCompress.Writer
{
    using SharpCompress.Common;
    using SharpCompress.Writer.GZip;
    using SharpCompress.Writer.Tar;
    using SharpCompress.Writer.Zip;
    using System;
    using System.IO;

    public static class WriterFactory
    {
        public static IWriter Open(Stream stream, ArchiveType archiveType, CompressionInfo compressionInfo)
        {
            switch (archiveType)
            {
                case ArchiveType.Zip:
                    return new ZipWriter(stream, compressionInfo, null);

                case ArchiveType.Tar:
                    return new TarWriter(stream, compressionInfo);

                case ArchiveType.GZip:
                    if (compressionInfo.Type != CompressionType.GZip)
                    {
                        throw new InvalidFormatException("GZip archives only support GZip compression type.");
                    }
                    return new GZipWriter(stream);
            }
            throw new NotSupportedException("Archive Type does not have a Writer: " + archiveType);
        }

        public static IWriter Open(Stream stream, ArchiveType archiveType, CompressionType compressionType)
        {
            CompressionInfo compressionInfo = new CompressionInfo();
            compressionInfo.Type = compressionType;
            return Open(stream, archiveType, compressionInfo);
        }
    }
}

