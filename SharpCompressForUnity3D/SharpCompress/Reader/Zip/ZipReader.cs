namespace SharpCompress.Reader.Zip
{
    using SharpCompress;
    using SharpCompress.Common;
    using SharpCompress.Common.Zip;
    using SharpCompress.Common.Zip.Headers;
    using SharpCompress.Reader;
    using System;
    using System.Collections;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.IO;
    using System.Runtime.CompilerServices;
    using System.Runtime.InteropServices;
    using System.Threading;

    public class ZipReader : AbstractReader<ZipEntry, ZipVolume>
    {
        private readonly StreamingZipHeaderFactory headerFactory;
        private readonly ZipVolume volume;

        internal ZipReader(Stream stream, Options options, string password) : base(options, ArchiveType.Zip)
        {
            this.volume = new ZipVolume(stream, options);
            this.headerFactory = new StreamingZipHeaderFactory(password);
        }

        internal override IEnumerable<ZipEntry> GetEntries(Stream stream)
        {
            foreach (ZipHeader iteratorVariable0 in this.headerFactory.ReadStreamHeader(stream))
            {
                if (iteratorVariable0 != null)
                {
                    switch (iteratorVariable0.ZipHeaderType)
                    {
                        case ZipHeaderType.LocalEntry:
                        {
                            yield return new ZipEntry(new StreamingZipFilePart(iteratorVariable0 as LocalEntryHeader, stream));
                            continue;
                        }
                        case ZipHeaderType.DirectoryEnd:
                            goto Label_00FC;
                    }
                }
            }
        Label_00FC:
            yield break;
        }
        public static ZipReader Open(Stream stream) {
            return Open(stream, null, Options.KeepStreamsOpen);
        }
        public static ZipReader Open(Stream stream, string password) {
            return Open(stream, password, Options.KeepStreamsOpen);
        }
        public static ZipReader Open(Stream stream,  string password,  Options options)
        {
            Utility.CheckNotNull(stream, "stream");
            return new ZipReader(stream, options, password);
        }

        public override ZipVolume Volume
        {
            get
            {
                return this.volume;
            }
        }

    }
}

