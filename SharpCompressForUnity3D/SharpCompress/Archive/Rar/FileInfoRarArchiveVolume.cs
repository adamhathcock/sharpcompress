namespace SharpCompress.Archive.Rar
{
    using SharpCompress;
    using SharpCompress.Common;
    using SharpCompress.Common.Rar;
    using SharpCompress.Common.Rar.Headers;
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Runtime.CompilerServices;
    using SharpCompress.IO;

    internal class FileInfoRarArchiveVolume : RarVolume
    {
        [CompilerGenerated]
        private System.IO.FileInfo _FileInfo_k__BackingField;
        [CompilerGenerated]
        private ReadOnlyCollection<RarFilePart> _FileParts_k__BackingField;

        internal FileInfoRarArchiveVolume(System.IO.FileInfo fileInfo, string password, Options options) : base(StreamingMode.Seekable, fileInfo.Open(FileMode.Open, FileAccess.Read), password, FixOptions(options))
        {
            this.FileInfo = fileInfo;
            this.FileParts = Utility.ToReadOnly<RarFilePart>(base.GetVolumeFileParts());
        }

        internal override RarFilePart CreateFilePart(FileHeader fileHeader, MarkHeader markHeader)
        {
            return new FileInfoRarFilePart(this, markHeader, fileHeader, this.FileInfo);
        }

        private static Options FixOptions(Options options)
        {
            if (options_HasFlag(options, Options.KeepStreamsOpen))
            {
                options = (Options) ((int) FlagUtility.SetFlag<Options>(options, Options.KeepStreamsOpen, false));
            }
            return options;
        }

        private static bool options_HasFlag(Options options, Options options2)
        {
            return ((options & options2) == options2);
        }

        internal override IEnumerable<RarFilePart> ReadFileParts()
        {
            return this.FileParts;
        }

        internal System.IO.FileInfo FileInfo
        {
            [CompilerGenerated]
            get
            {
                return this._FileInfo_k__BackingField;
            }
            [CompilerGenerated]
            private set
            {
                this._FileInfo_k__BackingField = value;
            }
        }

        internal ReadOnlyCollection<RarFilePart> FileParts
        {
            [CompilerGenerated]
            get
            {
                return this._FileParts_k__BackingField;
            }
            [CompilerGenerated]
            private set
            {
                this._FileParts_k__BackingField = value;
            }
        }
    }
}

