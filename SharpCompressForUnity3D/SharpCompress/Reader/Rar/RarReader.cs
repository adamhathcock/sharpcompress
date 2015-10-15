namespace SharpCompress.Reader.Rar
{
    using SharpCompress;
    using SharpCompress.Common;
    using SharpCompress.Common.Rar;
    using SharpCompress.Compressor.Rar;
    using SharpCompress.Reader;
    using System;
    using System.Collections;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.IO;
    using System.Linq;
    using System.Runtime.CompilerServices;
    using System.Runtime.InteropServices;
    using System.Threading;

    public abstract class RarReader : AbstractReader<RarReaderEntry, RarVolume>
    {
        [CompilerGenerated]
        private string _Password_k__BackingField;
        private readonly Unpack pack;
        private RarVolume volume;

        internal RarReader(Options options) : base(options, ArchiveType.Rar)
        {
            this.pack = new Unpack();
        }

        protected virtual IEnumerable<FilePart> CreateFilePartEnumerableForCurrentEntry()
        {
            return base.Entry.Parts;
        }

        internal override IEnumerable<RarReaderEntry> GetEntries(Stream stream)
        {
            this.volume = new RarReaderVolume(stream, this.Password, this.Options);
            foreach (RarFilePart iteratorVariable0 in this.volume.ReadFileParts())
            {
                this.ValidateArchive(this.volume);
                yield return new RarReaderEntry(this.volume.IsSolidArchive, iteratorVariable0);
            }
        }

        protected override EntryStream GetEntryStream()
        {
            return base.CreateEntryStream(new RarStream(this.pack, base.Entry.FileHeader, new MultiVolumeReadOnlyStream(Enumerable.Cast<RarFilePart>(this.CreateFilePartEnumerableForCurrentEntry()), this)));
        }
        public static RarReader Open(IEnumerable<Stream> streams) {
            return Open(streams,Options.KeepStreamsOpen);
        }
        public static RarReader Open(IEnumerable<Stream> streams,  Options options)
        {
            Utility.CheckNotNull(streams, "streams");
            return new MultiVolumeRarReader(streams, options);
        }
        public static RarReader Open(Stream stream) {
            return Open(stream, Options.KeepStreamsOpen);
        }
        public static RarReader Open(Stream stream,  Options options)
        {
            return Open(stream, null, options);
        }
        public static RarReader Open(Stream stream, string password) {
            return Open(stream,password, Options.KeepStreamsOpen);
        }
        public static RarReader Open(Stream stream, string password,  Options options)
        {
            Utility.CheckNotNull(stream, "stream");
            return new SingleVolumeRarReader(stream, password, options);
        }

        internal abstract void ValidateArchive(RarVolume archive);

        public string Password
        {
            [CompilerGenerated]
            get
            {
                return this._Password_k__BackingField;
            }
            [CompilerGenerated]
            set
            {
                this._Password_k__BackingField = value;
            }
        }

        public override RarVolume Volume
        {
            get
            {
                return this.volume;
            }
        }

    }
}

