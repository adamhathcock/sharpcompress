using System.Buffers.Binary;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace SharpCompress.Common.Zip.Headers;

internal class Zip64DirectoryEndHeader : ZipHeader
{
    public Zip64DirectoryEndHeader()
        : base(ZipHeaderType.Zip64DirectoryEnd) { }

    internal override void Read(BinaryReader reader)
    {
        SizeOfDirectoryEndRecord = (long)reader.ReadUInt64();
        VersionMadeBy = reader.ReadUInt16();
        VersionNeededToExtract = reader.ReadUInt16();
        VolumeNumber = reader.ReadUInt32();
        FirstVolumeWithDirectory = reader.ReadUInt32();
        TotalNumberOfEntriesInDisk = (long)reader.ReadUInt64();
        TotalNumberOfEntries = (long)reader.ReadUInt64();
        DirectorySize = (long)reader.ReadUInt64();
        DirectoryStartOffsetRelativeToDisk = (long)reader.ReadUInt64();
        DataSector = reader.ReadBytes(
            (int)(
                SizeOfDirectoryEndRecord
                - SIZE_OF_FIXED_HEADER_DATA_EXCEPT_SIGNATURE_AND_SIZE_FIELDS
            )
        );
    }

    internal async Task ReadAsync(Stream stream, CancellationToken cancellationToken)
    {
        SizeOfDirectoryEndRecord = (long)
            await ZipHeaderFactory.ReadUInt64Async(stream, cancellationToken).ConfigureAwait(false);
        VersionMadeBy = await ZipHeaderFactory.ReadUInt16Async(stream, cancellationToken).ConfigureAwait(false);
        VersionNeededToExtract = await ZipHeaderFactory.ReadUInt16Async(stream, cancellationToken)
            .ConfigureAwait(false);
        VolumeNumber = await ZipHeaderFactory.ReadUInt32Async(stream, cancellationToken).ConfigureAwait(false);
        FirstVolumeWithDirectory = await ZipHeaderFactory.ReadUInt32Async(stream, cancellationToken)
            .ConfigureAwait(false);
        TotalNumberOfEntriesInDisk = (long)
            await ZipHeaderFactory.ReadUInt64Async(stream, cancellationToken).ConfigureAwait(false);
        TotalNumberOfEntries = (long)
            await ZipHeaderFactory.ReadUInt64Async(stream, cancellationToken).ConfigureAwait(false);
        DirectorySize = (long)
            await ZipHeaderFactory.ReadUInt64Async(stream, cancellationToken).ConfigureAwait(false);
        DirectoryStartOffsetRelativeToDisk = (long)
            await ZipHeaderFactory.ReadUInt64Async(stream, cancellationToken).ConfigureAwait(false);
        DataSector = await ZipHeaderFactory.ReadBytesAsync(
                stream,
                (int)(
                    SizeOfDirectoryEndRecord
                    - SIZE_OF_FIXED_HEADER_DATA_EXCEPT_SIGNATURE_AND_SIZE_FIELDS
                ),
                cancellationToken
            )
            .ConfigureAwait(false);
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
