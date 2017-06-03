using System.IO;

namespace SharpCompress.Common.Tar
{
    public class TarVolume : GenericVolume
    {
        public TarVolume(Stream stream, Options options)
            : base(stream, options)
        {
        }

#if !PORTABLE
        public TarVolume(FileInfo fileInfo, Options options)
            : base(fileInfo, options)
        {
        }
#endif
    }
}
