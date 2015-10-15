namespace SharpCompress.Common
{
    using System;
    using System.Runtime.CompilerServices;

    public class CompressedBytesReadEventArgs : EventArgs
    {
        [CompilerGenerated]
        private long <CompressedBytesRead>k__BackingField;
        [CompilerGenerated]
        private long <CurrentFilePartCompressedBytesRead>k__BackingField;

        public long CompressedBytesRead
        {
            [CompilerGenerated]
            get
            {
                return this.<CompressedBytesRead>k__BackingField;
            }
            [CompilerGenerated]
            internal set
            {
                this.<CompressedBytesRead>k__BackingField = value;
            }
        }

        public long CurrentFilePartCompressedBytesRead
        {
            [CompilerGenerated]
            get
            {
                return this.<CurrentFilePartCompressedBytesRead>k__BackingField;
            }
            [CompilerGenerated]
            internal set
            {
                this.<CurrentFilePartCompressedBytesRead>k__BackingField = value;
            }
        }
    }
}

