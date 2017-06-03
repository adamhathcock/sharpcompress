using System.IO;

namespace SharpCompress.Common.Zip
{
    public class ZipVolume : GenericVolume
    {
        public ZipVolume(Stream stream, Options options)
            : base(stream, options)
        {
        }

#if !PORTABLE
        public ZipVolume(FileInfo fileInfo, Options options)
            : base(fileInfo, options)
        {
        }
#endif

        public string Comment { get; internal set; }
    }
}
