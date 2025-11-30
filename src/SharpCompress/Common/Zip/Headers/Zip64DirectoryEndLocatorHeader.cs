using System.Buffers.Binary;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace SharpCompress.Common.Zip.Headers;

internal class Zip64DirectoryEndLocatorHeader : ZipHeader
{
    public Zip64DirectoryEndLocatorHeader()
        : base(ZipHeaderType.Zip64DirectoryEndLocator) { }

    internal override void Read(BinaryReader reader)
    {
        FirstVolumeWithDirectory = reader.ReadUInt32();
        RelativeOffsetOfTheEndOfDirectoryRecord = (long)reader.ReadUInt64();
        TotalNumberOfVolumes = reader.ReadUInt32();
    }

    internal async Task ReadAsync(Stream stream, CancellationToken cancellationToken)
    {
        FirstVolumeWithDirectory = await ZipHeaderFactory.ReadUInt32Async(stream, cancellationToken)
            .ConfigureAwait(false);
        RelativeOffsetOfTheEndOfDirectoryRecord = (long)
            await ZipHeaderFactory.ReadUInt64Async(stream, cancellationToken).ConfigureAwait(false);
        TotalNumberOfVolumes = await ZipHeaderFactory.ReadUInt32Async(stream, cancellationToken)
            .ConfigureAwait(false);
    }

    public uint FirstVolumeWithDirectory { get; private set; }

    public long RelativeOffsetOfTheEndOfDirectoryRecord { get; private set; }

    public uint TotalNumberOfVolumes { get; private set; }
}
