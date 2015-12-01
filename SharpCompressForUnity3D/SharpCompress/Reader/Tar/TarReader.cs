namespace SharpCompress.Reader.Tar
{
    using SharpCompress;
    using SharpCompress.Archive.GZip;
    using SharpCompress.Archive.Tar;
    using SharpCompress.Common;
    using SharpCompress.Common.Tar;
    using SharpCompress.Compressor;
    using SharpCompress.Compressor.BZip2;
    using SharpCompress.Compressor.Deflate;
    using SharpCompress.IO;
    using SharpCompress.Reader;
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Runtime.InteropServices;

    public class TarReader : AbstractReader<TarEntry, TarVolume>
    {
        private readonly CompressionType compressionType;
        private readonly TarVolume volume;

        internal TarReader(Stream stream, CompressionType compressionType, Options options) : base(options, ArchiveType.Tar)
        {
            this.compressionType = compressionType;
            this.volume = new TarVolume(stream, options);
        }

        internal override IEnumerable<TarEntry> GetEntries(Stream stream)
        {
            return TarEntry.GetEntries(StreamingMode.Streaming, stream, this.compressionType);
        }
        public static TarReader Open(Stream stream) {
            return Open(stream, Options.KeepStreamsOpen);
        }
        public static TarReader Open(Stream stream,  Options options)
        {
            Utility.CheckNotNull(stream, "stream");
            RewindableStream stream2 = new RewindableStream(stream);
            stream2.StartRecording();
            if (GZipArchive.IsGZipFile(stream2))
            {
                stream2.Rewind(false);
                GZipStream stream3 = new GZipStream(stream2, CompressionMode.Decompress);
                if (!TarArchive.IsTarFile(stream3))
                {
                    throw new InvalidFormatException("Not a tar file.");
                }
                stream2.Rewind(true);
                return new TarReader(stream2, CompressionType.GZip, options);
            }
            stream2.Rewind(false);
            if (BZip2Stream.IsBZip2(stream2))
            {
                stream2.Rewind(false);
                BZip2Stream stream4 = new BZip2Stream(stream2, CompressionMode.Decompress, false, false);
                if (!TarArchive.IsTarFile(stream4))
                {
                    throw new InvalidFormatException("Not a tar file.");
                }
                stream2.Rewind(true);
                return new TarReader(stream2, CompressionType.BZip2, options);
            }
            stream2.Rewind(true);
            return new TarReader(stream2, CompressionType.None, options);
        }

        internal override Stream RequestInitialStream()
        {
            Stream stream = base.RequestInitialStream();
            switch (this.compressionType)
            {
                case CompressionType.None:
                    return stream;

                case CompressionType.GZip:
                    return new GZipStream(stream, CompressionMode.Decompress);

                case CompressionType.BZip2:
                    return new BZip2Stream(stream, CompressionMode.Decompress, false, false);
            }
            throw new NotSupportedException("Invalid compression type: " + this.compressionType);
        }

        public override TarVolume Volume
        {
            get
            {
                return this.volume;
            }
        }
    }
}

