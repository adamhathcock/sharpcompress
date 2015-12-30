using System.IO;

namespace SharpCompress.Common.GZip
{
    public class GZipVolume : Volume
    {
#if !DOTNET51
        private readonly FileInfo fileInfo;
#endif

        public GZipVolume(Stream stream, Options options)
            : base(stream, options)
        {
        }

#if !DOTNET51
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