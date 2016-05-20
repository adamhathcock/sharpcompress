using System;
using System.IO;
using SharpCompress.Common;
using SharpCompress.Writer.GZip;
using SharpCompress.Writer.Tar;
using SharpCompress.Writer.Zip;

namespace SharpCompress.Writer
{
    public static class WriterFactory
    {
        public static IWriter Open(Stream stream, ArchiveType archiveType, CompressionType compressionType, bool leaveOpen = false)
        {
            return Open(stream, archiveType, new CompressionInfo
                                                 {
                                                     Type = compressionType
                                                 }, leaveOpen);
        }

        public static IWriter Open(Stream stream, ArchiveType archiveType, CompressionInfo compressionInfo, bool leaveOpen = false)
        {
            switch (archiveType)
            {
                case ArchiveType.GZip:
                    {
                        if (compressionInfo.Type != CompressionType.GZip)
                        {
                            throw new InvalidFormatException("GZip archives only support GZip compression type.");
                        }
                        return new GZipWriter(stream, leaveOpen);
                    }
                case ArchiveType.Zip:
                    {
                        return new ZipWriter(stream, compressionInfo, null, leaveOpen);
                    }
                case ArchiveType.Tar:
                    {
                        return new TarWriter(stream, compressionInfo, leaveOpen);
                    }
                default:
                    {
                        throw new NotSupportedException("Archive Type does not have a Writer: " + archiveType);
                    }
            }
        }
    }
}