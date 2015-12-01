namespace SharpCompress.Common
{
    using System;
    using System.Runtime.CompilerServices;

    public class FilePartExtractionBeginEventArgs : EventArgs
    {
        [CompilerGenerated]
        private long _CompressedSize_k__BackingField;
        [CompilerGenerated]
        private string _Name_k__BackingField;
        [CompilerGenerated]
        private long _Size_k__BackingField;

        public long CompressedSize
        {
            [CompilerGenerated]
            get
            {
                return this._CompressedSize_k__BackingField;
            }
            [CompilerGenerated]
            internal set
            {
                this._CompressedSize_k__BackingField = value;
            }
        }

        public string Name
        {
            [CompilerGenerated]
            get
            {
                return this._Name_k__BackingField;
            }
            [CompilerGenerated]
            internal set
            {
                this._Name_k__BackingField = value;
            }
        }

        public long Size
        {
            [CompilerGenerated]
            get
            {
                return this._Size_k__BackingField;
            }
            [CompilerGenerated]
            internal set
            {
                this._Size_k__BackingField = value;
            }
        }
    }
}

