using System.IO;
using SharpCompress.Readers;

namespace SharpCompress.Common.GZip
{
    public class GZipVolume : Volume
    {
        public GZipVolume(Stream stream, ReaderOptions options)
            : base(stream, options)
        {
        }

#if !NO_FILE
        public GZipVolume(FileInfo fileInfo, ReaderOptions options)
            : base(fileInfo.OpenRead(), options)
        {
            options.LeaveStreamOpen = false;
        }
#endif

        public override bool IsFirstVolume { get { return true; } }

        public override bool IsMultiVolume { get { return true; } }
    }
}