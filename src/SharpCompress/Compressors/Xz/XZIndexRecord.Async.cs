using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace SharpCompress.Compressors.Xz;

public partial class XZIndexRecord
{
    public static async ValueTask<XZIndexRecord> FromBinaryReaderAsync(
        BinaryReader br,
        CancellationToken cancellationToken = default
    )
    {
        var record = new XZIndexRecord();
        record.UnpaddedSize = await br.ReadXZIntegerAsync(cancellationToken: cancellationToken)
            .ConfigureAwait(false);
        record.UncompressedSize = await br.ReadXZIntegerAsync(cancellationToken: cancellationToken)
            .ConfigureAwait(false);
        return record;
    }
}
