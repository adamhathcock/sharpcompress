namespace SharpCompress.Archive.Rar
{
    using SharpCompress.Common.Rar.Headers;
    using System;
    using System.IO;
    using System.Runtime.CompilerServices;

    internal class FileInfoRarFilePart : SeekableFilePart
    {
        [CompilerGenerated]
        private System.IO.FileInfo _FileInfo_k__BackingField;
        private readonly FileInfoRarArchiveVolume volume;

        internal FileInfoRarFilePart(FileInfoRarArchiveVolume volume, MarkHeader mh, FileHeader fh, System.IO.FileInfo fi) : base(mh, fh, volume.Stream, volume.Password)
        {
            this.volume = volume;
            this.FileInfo = fi;
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

        internal override string FilePartName
        {
            get
            {
                return ("Rar File: " + this.FileInfo.FullName + " File Entry: " + base.FileHeader.FileName);
            }
        }
    }
}

