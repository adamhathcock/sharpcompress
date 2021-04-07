using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace SharpCompress.Common.Zip.Headers
{
    internal class DirectoryEndHeader : ZipHeader
    {
        public DirectoryEndHeader()
            : base(ZipHeaderType.DirectoryEnd)
        {
        }

        internal override async ValueTask Read(Stream stream, CancellationToken cancellationToken)
        {
            VolumeNumber = await stream.ReadUInt16(cancellationToken);
            FirstVolumeWithDirectory = await stream.ReadUInt16(cancellationToken);
            TotalNumberOfEntriesInDisk = await stream.ReadUInt16(cancellationToken);
            TotalNumberOfEntries = await stream.ReadUInt16(cancellationToken);
            DirectorySize = await stream.ReadUInt32(cancellationToken);
            DirectoryStartOffsetRelativeToDisk = await stream.ReadUInt32(cancellationToken);
            CommentLength = await stream.ReadUInt16(cancellationToken);
            Comment = await stream.ReadBytes(CommentLength ?? 0, cancellationToken);
        }

        public ushort? VolumeNumber { get; private set; }

        public ushort? FirstVolumeWithDirectory { get; private set; }

        public ushort? TotalNumberOfEntriesInDisk { get; private set; }

        public uint? DirectorySize { get; private set; }

        public uint? DirectoryStartOffsetRelativeToDisk { get; private set; }

        public ushort? CommentLength { get; private set; }

        public byte[]? Comment { get; private set; }

        public ushort TotalNumberOfEntries { get; private set; }

        public bool IsZip64 => TotalNumberOfEntriesInDisk == ushort.MaxValue
                               || DirectorySize == uint.MaxValue
                               || DirectoryStartOffsetRelativeToDisk == uint.MaxValue;
    }
}