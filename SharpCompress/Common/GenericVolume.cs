using System.IO;

namespace SharpCompress.Common
{
    public class GenericVolume : Volume
    {
#if !PORTABLE && !NETFX_CORE
        private FileInfo fileInfo;
#endif

        public GenericVolume(Stream stream, Options options)
            : base(stream, options)
        {
        }

#if !PORTABLE && !NETFX_CORE
        public GenericVolume(FileInfo fileInfo, Options options)
            : base(fileInfo.OpenRead(), options)
        {
            this.fileInfo = fileInfo;
        }
#endif

#if !PORTABLE && !NETFX_CORE
        /// <summary>
        /// File that backs this volume, if it not stream based
        /// </summary>
        public override FileInfo VolumeFile
        {
            get { return fileInfo; }
        }
#endif

        public override bool IsFirstVolume
        {
            get { return true; }
        }

        public override bool IsMultiVolume
        {
            get { return true; }
        }
    }
}