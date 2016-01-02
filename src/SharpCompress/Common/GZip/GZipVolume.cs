using System.IO;

namespace SharpCompress.Common.GZip
{
    public class GZipVolume : Volume
    {
#if !NO_FILE
        private readonly FileInfo fileInfo;
#endif

        public GZipVolume(Stream stream, Options options)
            : base(stream, options)
        {
        }

#if !NO_FILE
        public GZipVolume(FileInfo fileInfo, Options options)
            : base(fileInfo.OpenRead(), options)
        {
            this.fileInfo = fileInfo;
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