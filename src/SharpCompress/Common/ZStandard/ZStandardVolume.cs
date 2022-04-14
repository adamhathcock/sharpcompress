using System.IO;
using SharpCompress.Readers;

namespace SharpCompress.Common.ZStandard
{
    public class ZStandardVolume : Volume
    {
        public ZStandardVolume(Stream stream, ReaderOptions options)
            : base(stream, options)
        {
        }

        public ZStandardVolume(FileInfo fileInfo, ReaderOptions options)
            : base(fileInfo.OpenRead(), options)
        {
            options.LeaveStreamOpen = false;
        }

        public override bool IsFirstVolume => true;

        public override bool IsMultiVolume => true;
    }
}