namespace SharpCompress.Compressor.PPMd.H
{
    using System;
    using System.Runtime.CompilerServices;

    internal abstract class Pointer
    {
        [CompilerGenerated]
        private int _Address_k__BackingField;
        [CompilerGenerated]
        private byte[] _Memory_k__BackingField;

        internal Pointer(byte[] mem)
        {
            this.Memory = mem;
        }

        protected T Initialize<T>(byte[] mem) where T: Pointer
        {
            this.Memory = mem;
            this.Address = 0;
            return (this as T);
        }

        internal virtual int Address
        {
            [CompilerGenerated]
            get
            {
                return this._Address_k__BackingField;
            }
            [CompilerGenerated]
            set
            {
                this._Address_k__BackingField = value;
            }
        }

        internal byte[] Memory
        {
            [CompilerGenerated]
            get
            {
                return this._Memory_k__BackingField;
            }
            [CompilerGenerated]
            private set
            {
                this._Memory_k__BackingField = value;
            }
        }
    }
}

