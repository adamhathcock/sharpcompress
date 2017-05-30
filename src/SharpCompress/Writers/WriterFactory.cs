using System;
using System.IO;
using SharpCompress.Common;
using SharpCompress.Writers.GZip;
using SharpCompress.Writers.LZip;
using SharpCompress.Writers.Tar;
using SharpCompress.Writers.Zip;

namespace SharpCompress.Writers
{
    public static class WriterFactory
    {
        public static IWriter Open(Stream stream, ArchiveType archiveType, WriterOptions writerOptions)
        {
            switch (archiveType)
            {
                case ArchiveType.GZip:
                {
                    if (writerOptions.CompressionType != CompressionType.GZip)
                    {
                        throw new InvalidFormatException("GZip archives only support GZip compression type.");
                    }
                    return new GZipWriter(stream, writerOptions.LeaveStreamOpen);
                }
                case ArchiveType.Zip:
                {
                    return new ZipWriter(stream, new ZipWriterOptions(writerOptions));
                }
                case ArchiveType.Tar:
                {
                    return new TarWriter(stream, writerOptions);
                }
                case ArchiveType.LZip:
                {
                    if (writerOptions.CompressionType != CompressionType.LZip)
                    {
                        throw new InvalidFormatException("LZip archives only support LZip compression type.");
                    }
                    return new LZipWriter(stream, writerOptions.LeaveStreamOpen);
                }
                default:
                {
                    throw new NotSupportedException("Archive Type does not have a Writer: " + archiveType);
                }
            }
        }
    }
}