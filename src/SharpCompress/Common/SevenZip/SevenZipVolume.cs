using System.IO;

namespace SharpCompress.Common.SevenZip
{
    public class SevenZipVolume : Volume
    {
        public SevenZipVolume(Stream stream, Options options)
            : base(stream, options)
        {
        }
    }
}