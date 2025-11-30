using System.Buffers.Binary;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace SharpCompress.Common.Zip.Headers;

internal class DirectoryEndHeader : ZipHeader
{
    public DirectoryEndHeader()
        : base(ZipHeaderType.DirectoryEnd) { }

    internal override void Read(BinaryReader reader)
    {
        VolumeNumber = reader.ReadUInt16();
        FirstVolumeWithDirectory = reader.ReadUInt16();
        TotalNumberOfEntriesInDisk = reader.ReadUInt16();
        TotalNumberOfEntries = reader.ReadUInt16();
        DirectorySize = reader.ReadUInt32();
        DirectoryStartOffsetRelativeToDisk = reader.ReadUInt32();
        CommentLength = reader.ReadUInt16();
        Comment = reader.ReadBytes(CommentLength);
    }

    internal async Task ReadAsync(Stream stream, CancellationToken cancellationToken)
    {
        VolumeNumber = await ZipHeaderFactory.ReadUInt16Async(stream, cancellationToken).ConfigureAwait(false);
        FirstVolumeWithDirectory = await ZipHeaderFactory.ReadUInt16Async(stream, cancellationToken)
            .ConfigureAwait(false);
        TotalNumberOfEntriesInDisk = await ZipHeaderFactory.ReadUInt16Async(stream, cancellationToken)
            .ConfigureAwait(false);
        TotalNumberOfEntries = await ZipHeaderFactory.ReadUInt16Async(stream, cancellationToken)
            .ConfigureAwait(false);
        DirectorySize = await ZipHeaderFactory.ReadUInt32Async(stream, cancellationToken).ConfigureAwait(false);
        DirectoryStartOffsetRelativeToDisk = await ZipHeaderFactory.ReadUInt32Async(stream, cancellationToken)
            .ConfigureAwait(false);
        CommentLength = await ZipHeaderFactory.ReadUInt16Async(stream, cancellationToken).ConfigureAwait(false);
        Comment = await ZipHeaderFactory.ReadBytesAsync(stream, CommentLength, cancellationToken).ConfigureAwait(false);
    }

    public ushort VolumeNumber { get; private set; }

    public ushort FirstVolumeWithDirectory { get; private set; }

    public ushort TotalNumberOfEntriesInDisk { get; private set; }

    public uint DirectorySize { get; private set; }

    public uint DirectoryStartOffsetRelativeToDisk { get; private set; }

    public ushort CommentLength { get; private set; }

    public byte[]? Comment { get; private set; }

    public ushort TotalNumberOfEntries { get; private set; }

    public bool IsZip64 =>
        TotalNumberOfEntriesInDisk == ushort.MaxValue
        || DirectorySize == uint.MaxValue
        || DirectoryStartOffsetRelativeToDisk == uint.MaxValue;
}
