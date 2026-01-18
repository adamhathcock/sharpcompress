using System.Threading;
using System.Threading.Tasks;
using SharpCompress.Common.Rar;
using SharpCompress.IO;

namespace SharpCompress.Common.Rar.Headers;

// ProtectHeader is part of the Recovery Record feature
internal sealed class ProtectHeader : RarHeader
{
    public ProtectHeader(RarHeader header, RarCrcBinaryReader reader)
        : base(header, reader, HeaderType.Protect)
    {
        if (IsRar5)
        {
            throw new InvalidFormatException("unexpected rar5 record");
        }
    }

    protected override void ReadFinish(MarkingBinaryReader reader)
    {
        Version = reader.ReadByte();
        RecSectors = reader.ReadUInt16();
        TotalBlocks = reader.ReadUInt32();
        Mark = reader.ReadBytes(8);
    }

    protected override async ValueTask ReadFinishAsync(
        AsyncMarkingBinaryReader reader,
        CancellationToken cancellationToken = default
    )
    {
        Version = await reader.ReadByteAsync(cancellationToken).ConfigureAwait(false);
        RecSectors = await reader.ReadUInt16Async(cancellationToken).ConfigureAwait(false);
        TotalBlocks = await reader.ReadUInt32Async(cancellationToken).ConfigureAwait(false);
        Mark = await reader.ReadBytesAsync(8, cancellationToken).ConfigureAwait(false);
    }

    internal uint DataSize => checked((uint)AdditionalDataSize);
    internal byte Version { get; private set; }
    internal ushort RecSectors { get; private set; }
    internal uint TotalBlocks { get; private set; }
    internal byte[]? Mark { get; private set; }
}
