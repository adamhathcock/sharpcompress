namespace SharpCompress.Common
{
    using System;
    using System.Runtime.CompilerServices;

    public class CompressedBytesReadEventArgs : EventArgs
    {
        [CompilerGenerated]
        private long _CompressedBytesRead_k__BackingField;
        [CompilerGenerated]
        private long _CurrentFilePartCompressedBytesRead_k__BackingField;

        public long CompressedBytesRead
        {
            [CompilerGenerated]
            get
            {
                return this._CompressedBytesRead_k__BackingField;
            }
            [CompilerGenerated]
            internal set
            {
                this._CompressedBytesRead_k__BackingField = value;
            }
        }

        public long CurrentFilePartCompressedBytesRead
        {
            [CompilerGenerated]
            get
            {
                return this._CurrentFilePartCompressedBytesRead_k__BackingField;
            }
            [CompilerGenerated]
            internal set
            {
                this._CurrentFilePartCompressedBytesRead_k__BackingField = value;
            }
        }
    }
}

