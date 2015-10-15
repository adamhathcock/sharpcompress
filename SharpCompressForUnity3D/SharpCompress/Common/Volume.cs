namespace SharpCompress.Common
{
    using SharpCompress.IO;
    using System;
    using System.IO;
    using System.Runtime.CompilerServices;

    public abstract class Volume : IVolume, IDisposable
    {
        [CompilerGenerated]
        private SharpCompress.Common.Options <Options>k__BackingField;
        private readonly System.IO.Stream actualStream;
        private bool disposed;

        internal Volume(System.IO.Stream stream, SharpCompress.Common.Options options)
        {
            this.actualStream = stream;
            this.Options = options;
        }

        public void Dispose()
        {
            if (!(this.Options_HasFlag(SharpCompress.Common.Options.KeepStreamsOpen) || this.disposed))
            {
                this.actualStream.Dispose();
                this.disposed = true;
            }
        }

        private bool Options_HasFlag(SharpCompress.Common.Options options)
        {
            return ((this.Options & options) == options);
        }

        public virtual bool IsFirstVolume
        {
            get
            {
                return true;
            }
        }

        public virtual bool IsMultiVolume
        {
            get
            {
                return true;
            }
        }

        internal SharpCompress.Common.Options Options
        {
            [CompilerGenerated]
            get
            {
                return this.<Options>k__BackingField;
            }
            [CompilerGenerated]
            private set
            {
                this.<Options>k__BackingField = value;
            }
        }

        internal System.IO.Stream Stream
        {
            get
            {
                return new NonDisposingStream(this.actualStream);
            }
        }
    }
}

