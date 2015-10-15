namespace SharpCompress.Common
{
    using System;
    using System.Runtime.CompilerServices;

    public class ReaderExtractionEventArgs<T> : EventArgs
    {
        [CompilerGenerated]
        private T <Item>k__BackingField;

        internal ReaderExtractionEventArgs(T entry)
        {
            this.Item = entry;
        }

        public T Item
        {
            [CompilerGenerated]
            get
            {
                return this.<Item>k__BackingField;
            }
            [CompilerGenerated]
            private set
            {
                this.<Item>k__BackingField = value;
            }
        }
    }
}

