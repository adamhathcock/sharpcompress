namespace SharpCompress.Common
{
    using System;
    using System.Runtime.CompilerServices;

    public class ReaderExtractionEventArgs<T> : EventArgs
    {
        [CompilerGenerated]
        private T _Item_k__BackingField;

        internal ReaderExtractionEventArgs(T entry)
        {
            this.Item = entry;
        }

        public T Item
        {
            [CompilerGenerated]
            get
            {
                return this._Item_k__BackingField;
            }
            [CompilerGenerated]
            private set
            {
                this._Item_k__BackingField = value;
            }
        }
    }
}

