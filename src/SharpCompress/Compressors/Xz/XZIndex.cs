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

[CLSCompliant(false)]
public class XZIndex
{
    private readonly BinaryReader _reader;
    public long StreamStartPosition { get; private set; }
    public ulong NumberOfRecords { get; private set; }
    public List<XZIndexRecord> Records { get; } = new();

    private readonly bool _indexMarkerAlreadyVerified;

    public XZIndex(BinaryReader reader, bool indexMarkerAlreadyVerified)
    {
        _reader = reader;
        _indexMarkerAlreadyVerified = indexMarkerAlreadyVerified;
        StreamStartPosition = reader.BaseStream.Position;
        if (indexMarkerAlreadyVerified)
        {
            StreamStartPosition--;
        }
    }

    public static XZIndex FromStream(Stream stream, bool indexMarkerAlreadyVerified)
    {
        var index = new XZIndex(
            new BinaryReader(stream, Encoding.UTF8, true),
            indexMarkerAlreadyVerified
        );
        index.Process();
        return index;
    }

    public static async Task<XZIndex> FromStreamAsync(
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

    public void Process()
    {
        if (!_indexMarkerAlreadyVerified)
        {
            VerifyIndexMarker();
        }

        NumberOfRecords = _reader.ReadXZInteger();
        for (ulong i = 0; i < NumberOfRecords; i++)
        {
            Records.Add(XZIndexRecord.FromBinaryReader(_reader));
        }
        SkipPadding();
        VerifyCrc32();
    }

    public async Task ProcessAsync(CancellationToken cancellationToken = default)
    {
        if (!_indexMarkerAlreadyVerified)
        {
            await VerifyIndexMarkerAsync(cancellationToken).ConfigureAwait(false);
        }

        NumberOfRecords = await _reader.ReadXZIntegerAsync(cancellationToken).ConfigureAwait(false);
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

    private void VerifyIndexMarker()
    {
        var marker = _reader.ReadByte();
        if (marker != 0)
        {
            throw new InvalidFormatException("Not an index block");
        }
    }

    private async Task VerifyIndexMarkerAsync(CancellationToken cancellationToken = default)
    {
        var marker = await _reader.ReadByteAsync(cancellationToken).ConfigureAwait(false);
        if (marker != 0)
        {
            throw new InvalidFormatException("Not an index block");
        }
    }

    private void SkipPadding()
    {
        var bytes = (int)(_reader.BaseStream.Position - StreamStartPosition) % 4;
        if (bytes > 0)
        {
            var paddingBytes = _reader.ReadBytes(4 - bytes);
            if (paddingBytes.Any(b => b != 0))
            {
                throw new InvalidFormatException("Padding bytes were non-null");
            }
        }
    }

    private async Task SkipPaddingAsync(CancellationToken cancellationToken = default)
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

    private void VerifyCrc32()
    {
        var crc = _reader.ReadLittleEndianUInt32();
        // TODO verify this matches
    }

    private async Task VerifyCrc32Async(CancellationToken cancellationToken = default)
    {
        var crc = await _reader
            .BaseStream.ReadLittleEndianUInt32Async(cancellationToken)
            .ConfigureAwait(false);
        // TODO verify this matches
    }
}
