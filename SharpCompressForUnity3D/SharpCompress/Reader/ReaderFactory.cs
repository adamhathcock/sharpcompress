namespace SharpCompress.Reader
{
    using SharpCompress;
    using SharpCompress.Archive.GZip;
    using SharpCompress.Archive.Rar;
    using SharpCompress.Archive.Tar;
    using SharpCompress.Archive.Zip;
    using SharpCompress.Common;
    using SharpCompress.Compressor;
    using SharpCompress.Compressor.BZip2;
    using SharpCompress.Compressor.Deflate;
    using SharpCompress.IO;
    using SharpCompress.Reader.GZip;
    using SharpCompress.Reader.Rar;
    using SharpCompress.Reader.Tar;
    using SharpCompress.Reader.Zip;
    using System;
    using System.IO;
    using System.Runtime.InteropServices;

    public static class ReaderFactory
    {
        public static IReader Open(Stream stream, [Optional, DefaultParameterValue(1)] Options options)
        {
            Utility.CheckNotNull(stream, "stream");
            RewindableStream stream2 = new RewindableStream(stream);
            stream2.StartRecording();
            if (ZipArchive.IsZipFile(stream2, null))
            {
                stream2.Rewind(true);
                return ZipReader.Open(stream2, null, options);
            }
            stream2.Rewind(false);
            if (GZipArchive.IsGZipFile(stream2))
            {
                stream2.Rewind(false);
                GZipStream stream3 = new GZipStream(stream2, CompressionMode.Decompress);
                if (TarArchive.IsTarFile(stream3))
                {
                    stream2.Rewind(true);
                    return new TarReader(stream2, CompressionType.GZip, options);
                }
                stream2.Rewind(true);
                return GZipReader.Open(stream2, options);
            }
            stream2.Rewind(false);
            if (BZip2Stream.IsBZip2(stream2))
            {
                stream2.Rewind(false);
                BZip2Stream stream4 = new BZip2Stream(stream2, CompressionMode.Decompress, false, false);
                if (TarArchive.IsTarFile(stream4))
                {
                    stream2.Rewind(true);
                    return new TarReader(stream2, CompressionType.BZip2, options);
                }
            }
            stream2.Rewind(false);
            if (TarArchive.IsTarFile(stream2))
            {
                stream2.Rewind(true);
                return TarReader.Open(stream2, options);
            }
            stream2.Rewind(false);
            if (!RarArchive.IsRarFile(stream2, options))
            {
                throw new InvalidOperationException("Cannot determine compressed stream type.  Supported Reader Formats: Zip, GZip, BZip2, Tar, Rar");
            }
            stream2.Rewind(true);
            return RarReader.Open(stream2, options);
        }
    }
}

