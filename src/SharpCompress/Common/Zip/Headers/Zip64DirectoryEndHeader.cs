using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace SharpCompress.Common.Zip.Headers
{
    internal class Zip64DirectoryEndHeader : ZipHeader
    {
        public Zip64DirectoryEndHeader()
            : base(ZipHeaderType.Zip64DirectoryEnd)
        {
        }

        internal override async ValueTask Read(Stream stream, CancellationToken cancellationToken)
        {
            SizeOfDirectoryEndRecord = (long)await stream.ReadUInt64(cancellationToken);
            VersionMadeBy = await stream.ReadUInt16(cancellationToken);
            VersionNeededToExtract = await stream.ReadUInt16(cancellationToken);
            VolumeNumber = await stream.ReadUInt32(cancellationToken);
            FirstVolumeWithDirectory = await stream.ReadUInt32(cancellationToken);
            TotalNumberOfEntriesInDisk = (long)await stream.ReadUInt64(cancellationToken);
            TotalNumberOfEntries = (long)await stream.ReadUInt64(cancellationToken);
            DirectorySize = (long)await stream.ReadUInt64(cancellationToken);
            DirectoryStartOffsetRelativeToDisk = (long)await stream.ReadUInt64(cancellationToken);
            DataSector = await stream.ReadBytes((int)(SizeOfDirectoryEndRecord - SIZE_OF_FIXED_HEADER_DATA_EXCEPT_SIGNATURE_AND_SIZE_FIELDS), cancellationToken);
        }

        private const int SIZE_OF_FIXED_HEADER_DATA_EXCEPT_SIGNATURE_AND_SIZE_FIELDS = 44;

        public long SizeOfDirectoryEndRecord { get; private set; }

        public ushort VersionMadeBy { get; private set; }

        public ushort VersionNeededToExtract { get; private set; }

        public uint VolumeNumber { get; private set; }

        public uint FirstVolumeWithDirectory { get; private set; }

        public long TotalNumberOfEntriesInDisk { get; private set; }

        public long TotalNumberOfEntries { get; private set; }

        public long DirectorySize { get; private set; }

        public long DirectoryStartOffsetRelativeToDisk { get; private set; }

        public byte[]? DataSector { get; private set; }
    }
}