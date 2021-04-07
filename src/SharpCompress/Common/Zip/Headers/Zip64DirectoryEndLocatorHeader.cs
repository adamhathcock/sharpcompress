using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace SharpCompress.Common.Zip.Headers
{
    internal class Zip64DirectoryEndLocatorHeader : ZipHeader
    {
        public Zip64DirectoryEndLocatorHeader()
            : base(ZipHeaderType.Zip64DirectoryEndLocator)
        {
        }

        internal override async ValueTask Read(Stream stream, CancellationToken cancellationToken)
        {
            FirstVolumeWithDirectory = await stream.ReadUInt32(cancellationToken);
            RelativeOffsetOfTheEndOfDirectoryRecord = (long)await stream.ReadUInt64(cancellationToken);
            TotalNumberOfVolumes = await stream.ReadUInt32(cancellationToken);
        }

        public uint FirstVolumeWithDirectory { get; private set; }

        public long RelativeOffsetOfTheEndOfDirectoryRecord { get; private set; }

        public uint TotalNumberOfVolumes { get; private set; }
    }
}