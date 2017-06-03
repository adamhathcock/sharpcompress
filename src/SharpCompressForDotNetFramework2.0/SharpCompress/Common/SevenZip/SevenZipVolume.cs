using System.IO;

namespace SharpCompress.Common.SevenZip
{
    public class SevenZipVolume : GenericVolume
    {
        public SevenZipVolume(Stream stream, Options options)
            : base(stream, options)
        {
        }

#if !PORTABLE
        public SevenZipVolume(FileInfo fileInfo, Options options)
            : base(fileInfo, options)
        {
        }
#endif
    }
}
