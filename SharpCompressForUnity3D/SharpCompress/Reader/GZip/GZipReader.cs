namespace SharpCompress.Reader.GZip
{
    using SharpCompress;
    using SharpCompress.Common;
    using SharpCompress.Common.GZip;
    using SharpCompress.Reader;
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Runtime.InteropServices;

    public class GZipReader : AbstractReader<GZipEntry, GZipVolume>
    {
        private readonly GZipVolume volume;

        internal GZipReader(Stream stream, Options options) : base(options, ArchiveType.GZip)
        {
            this.volume = new GZipVolume(stream, options);
        }

        internal override IEnumerable<GZipEntry> GetEntries(Stream stream)
        {
            return GZipEntry.GetEntries(stream);
        }

        public static GZipReader Open(Stream stream, [Optional, DefaultParameterValue(1)] Options options)
        {
            Utility.CheckNotNull(stream, "stream");
            return new GZipReader(stream, options);
        }

        public override GZipVolume Volume
        {
            get
            {
                return this.volume;
            }
        }
    }
}

