using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using SharpCompress.Common;
using SharpCompress.IO;

namespace SharpCompress.Compressors.Xz;

public partial class XZIndex
{
    public static async ValueTask<XZIndex> FromStreamAsync(
        Stream stream,
        bool indexMarkerAlreadyVerified,
        CancellationToken cancellationToken = default
    )
    {
        var index = new XZIndex(
            new BinaryReader(stream, Encoding.UTF8, true),
            indexMarkerAlreadyVerified
        );
        await index.ProcessAsync(cancellationToken).ConfigureAwait(false);
        return index;
    }

    public async ValueTask ProcessAsync(CancellationToken cancellationToken = default)
    {
        if (!_indexMarkerAlreadyVerified)
        {
            await VerifyIndexMarkerAsync(cancellationToken).ConfigureAwait(false);
        }

        NumberOfRecords = await _reader
            .ReadXZIntegerAsync(cancellationToken: cancellationToken)
            .ConfigureAwait(false);
        for (ulong i = 0; i < NumberOfRecords; i++)
        {
            Records.Add(
                await XZIndexRecord
                    .FromBinaryReaderAsync(_reader, cancellationToken)
                    .ConfigureAwait(false)
            );
        }
        await SkipPaddingAsync(cancellationToken).ConfigureAwait(false);
        await VerifyCrc32Async(cancellationToken).ConfigureAwait(false);
    }

    private async ValueTask VerifyIndexMarkerAsync(CancellationToken cancellationToken = default)
    {
        var marker = await _reader.ReadByteAsync(cancellationToken).ConfigureAwait(false);
        if (marker != 0)
        {
            throw new InvalidFormatException("Not an index block");
        }
    }

    private async ValueTask SkipPaddingAsync(CancellationToken cancellationToken = default)
    {
        var bytes = (int)(_reader.BaseStream.Position - StreamStartPosition) % 4;
        if (bytes > 0)
        {
            var paddingBytes = await _reader
                .ReadBytesAsync(4 - bytes, cancellationToken)
                .ConfigureAwait(false);
            if (paddingBytes.Any(b => b != 0))
            {
                throw new InvalidFormatException("Padding bytes were non-null");
            }
        }
    }

    private async ValueTask VerifyCrc32Async(CancellationToken cancellationToken = default)
    {
        var crc = await _reader
            .BaseStream.ReadLittleEndianUInt32Async(cancellationToken)
            .ConfigureAwait(false);
        // TODO verify this matches
    }
}
