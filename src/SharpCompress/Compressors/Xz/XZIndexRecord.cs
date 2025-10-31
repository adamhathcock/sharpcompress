using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace SharpCompress.Compressors.Xz;

[CLSCompliant(false)]
public class XZIndexRecord
{
    public ulong UnpaddedSize { get; private set; }
    public ulong UncompressedSize { get; private set; }

    protected XZIndexRecord() { }

    public static XZIndexRecord FromBinaryReader(BinaryReader br)
    {
        var record = new XZIndexRecord();
        record.UnpaddedSize = br.ReadXZInteger();
        record.UncompressedSize = br.ReadXZInteger();
        return record;
    }

    public static async Task<XZIndexRecord> FromBinaryReaderAsync(
        BinaryReader br,
        CancellationToken cancellationToken = default
    )
    {
        var record = new XZIndexRecord();
        record.UnpaddedSize = await br.ReadXZIntegerAsync(cancellationToken).ConfigureAwait(false);
        record.UncompressedSize = await br.ReadXZIntegerAsync(cancellationToken)
            .ConfigureAwait(false);
        return record;
    }
}
