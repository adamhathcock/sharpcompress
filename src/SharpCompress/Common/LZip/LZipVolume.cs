using System.IO;
using SharpCompress.Readers;

namespace SharpCompress.Common.LZip
{
    public class LZipVolume : Volume
    {
        public LZipVolume(Stream stream, ReaderOptions options)
            : base(stream, options)
        {
        }

#if !NO_FILE
        public LZipVolume(FileInfo fileInfo, ReaderOptions options)
            : base(fileInfo.OpenRead(), options)
        {
            options.LeaveStreamOpen = false;
        }
#endif

        public override bool IsFirstVolume => true;

        public override bool IsMultiVolume => true;
    }
}