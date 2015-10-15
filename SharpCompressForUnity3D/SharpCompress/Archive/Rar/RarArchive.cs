namespace SharpCompress.Archive.Rar
{
    using SharpCompress;
    using SharpCompress.Archive;
    using SharpCompress.Common;
    using SharpCompress.Common.Rar.Headers;
    using SharpCompress.Compressor.Rar;
    using SharpCompress.IO;
    using SharpCompress.Reader;
    using SharpCompress.Reader.Rar;
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Runtime.InteropServices;

    public class RarArchive : AbstractArchive<RarArchiveEntry, RarVolume>
    {
        private readonly SharpCompress.Compressor.Rar.Unpack unpack;

        internal RarArchive(IEnumerable<Stream> streams, Options options, string password) : base(ArchiveType.Rar, streams, options, password)
        {
            this.unpack = new SharpCompress.Compressor.Rar.Unpack();
        }

        protected override IReader CreateReaderForSolidExtraction()
        {
            Stream stream = Enumerable.First<RarVolume>(base.Volumes).Stream;
            stream.Position = 0L;
            return RarReader.Open(stream, Options.KeepStreamsOpen);
        }

        public static bool IsRarFile(Stream stream)
        {
            return IsRarFile(stream, Options.None);
        }

        public static bool IsRarFile(Stream stream, Options options)
        {
            try
            {
                RarHeaderFactory factory = new RarHeaderFactory(StreamingMode.Seekable, options, null);
                RarHeader header = Enumerable.FirstOrDefault<RarHeader>(factory.ReadHeaders(stream));
                if (header == null)
                {
                    return false;
                }
                return Enum.IsDefined(typeof(HeaderType), header.HeaderType);
            }
            catch
            {
                return false;
            }
        }

        protected override IEnumerable<RarArchiveEntry> LoadEntries(IEnumerable<RarVolume> volumes)
        {
            return RarArchiveEntryFactory.GetEntries(this, volumes);
        }

        protected override IEnumerable<RarVolume> LoadVolumes(IEnumerable<Stream> streams, Options options)
        {
            return RarArchiveVolumeFactory.GetParts(streams, base.Password, options);
        }

        public static RarArchive Open(IEnumerable<Stream> streams, [Optional, DefaultParameterValue(1)] Options options, [Optional, DefaultParameterValue(null)] string password)
        {
            Utility.CheckNotNull(streams, "streams");
            return new RarArchive(streams, options, password);
        }

        public static RarArchive Open(Stream stream, [Optional, DefaultParameterValue(1)] Options options, [Optional, DefaultParameterValue(null)] string password)
        {
            Utility.CheckNotNull(stream, "stream");
            return Open(Utility.AsEnumerable<Stream>(stream), options, password);
        }

        public override bool IsSolid
        {
            get
            {
                return Enumerable.First<RarVolume>(base.Volumes).IsSolidArchive;
            }
        }

        internal SharpCompress.Compressor.Rar.Unpack Unpack
        {
            get
            {
                return this.unpack;
            }
        }
    }
}

