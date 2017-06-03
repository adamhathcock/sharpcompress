using System.IO;
using SharpCompress.IO;

namespace SharpCompress.Common
{
    public abstract class Volume : IVolume
    {
        private readonly Stream actualStream;

        internal Volume(Stream stream, Options options)
        {
            actualStream = stream;
            Options = options;
        }

        internal Stream Stream
        {
            get
            {
                return new NonDisposingStream(actualStream);
            }
        }

        internal Options Options { get; private set; }

        /// <summary>
        /// RarArchive is the first volume of a multi-part archive.
        /// Only Rar 3.0 format and higher
        /// </summary>
        public abstract bool IsFirstVolume
        {
            get;
        }

        /// <summary>
        /// RarArchive is part of a multi-part archive.
        /// </summary>
        public abstract bool IsMultiVolume
        {
            get;
        }

#if !PORTABLE
        public abstract FileInfo VolumeFile
        {
            get;
        }
#endif

        private bool disposed;
        public void Dispose()
        {
            if (!Options.HasFlag(Options.KeepStreamsOpen) && !disposed)
            {
                actualStream.Dispose();
                disposed = true;
            }
        }
    }
}
