namespace SharpCompress.Common.Rar
{
    using SharpCompress.Common;
    using SharpCompress.Common.Rar.Headers;
    using System;
    using System.IO;
    using System.Runtime.CompilerServices;

    internal abstract class RarFilePart : FilePart
    {
        [CompilerGenerated]
        private SharpCompress.Common.Rar.Headers.FileHeader <FileHeader>k__BackingField;
        [CompilerGenerated]
        private SharpCompress.Common.Rar.Headers.MarkHeader <MarkHeader>k__BackingField;

        internal RarFilePart(SharpCompress.Common.Rar.Headers.MarkHeader mh, SharpCompress.Common.Rar.Headers.FileHeader fh)
        {
            this.MarkHeader = mh;
            this.FileHeader = fh;
        }

        internal override Stream GetRawStream()
        {
            return null;
        }

        internal SharpCompress.Common.Rar.Headers.FileHeader FileHeader
        {
            [CompilerGenerated]
            get
            {
                return this.<FileHeader>k__BackingField;
            }
            [CompilerGenerated]
            private set
            {
                this.<FileHeader>k__BackingField = value;
            }
        }

        internal SharpCompress.Common.Rar.Headers.MarkHeader MarkHeader
        {
            [CompilerGenerated]
            get
            {
                return this.<MarkHeader>k__BackingField;
            }
            [CompilerGenerated]
            private set
            {
                this.<MarkHeader>k__BackingField = value;
            }
        }
    }
}

