using System.IO;
using SharpCompress.Readers;

namespace SharpCompress.Common.Xz
{
    public class XzVolume : Volume
    {
        public XzVolume(Stream stream, ReaderOptions options)
            : base(stream, options)
        {
        }

#if !NO_FILE
        public XzVolume(FileInfo fileInfo, ReaderOptions options)
            : base(fileInfo.OpenRead(), options)
        {
            options.LeaveStreamOpen = false;
        }
#endif

        public override bool IsFirstVolume => true;

        public override bool IsMultiVolume => true;
    }
}