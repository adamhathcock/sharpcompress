using System;
using System.Collections.Generic;
using System.IO;
using SharpCompress.Common;
using SharpCompress.Common.ZStandard;

namespace SharpCompress.Readers.Tar
{
    public class ZStandardReader : AbstractReader<ZStandardEntry, ZStandardVolume>
    {
        private readonly CompressionType compressionType;

        internal ZStandardReader(Stream stream, ReaderOptions options, CompressionType compressionType)
            : base(options, ArchiveType.Tar)
        {
            this.compressionType = compressionType;
            Volume = new ZStandardVolume(stream, options);
        }

        public override ZStandardVolume Volume { get; }

        protected override Stream RequestInitialStream()
        {
            throw new NotImplementedException();
        }

        protected override IEnumerable<ZStandardEntry> GetEntries(Stream stream)
        {
            throw new NotImplementedException();
        }
    }
}
