namespace SharpCompress.Common
{
    using System;
    using System.Runtime.CompilerServices;

    public class FilePartExtractionBeginEventArgs : EventArgs
    {
        [CompilerGenerated]
        private long <CompressedSize>k__BackingField;
        [CompilerGenerated]
        private string <Name>k__BackingField;
        [CompilerGenerated]
        private long <Size>k__BackingField;

        public long CompressedSize
        {
            [CompilerGenerated]
            get
            {
                return this.<CompressedSize>k__BackingField;
            }
            [CompilerGenerated]
            internal set
            {
                this.<CompressedSize>k__BackingField = value;
            }
        }

        public string Name
        {
            [CompilerGenerated]
            get
            {
                return this.<Name>k__BackingField;
            }
            [CompilerGenerated]
            internal set
            {
                this.<Name>k__BackingField = value;
            }
        }

        public long Size
        {
            [CompilerGenerated]
            get
            {
                return this.<Size>k__BackingField;
            }
            [CompilerGenerated]
            internal set
            {
                this.<Size>k__BackingField = value;
            }
        }
    }
}

