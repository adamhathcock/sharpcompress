using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using SharpCompress.Common;
using SharpCompress.Compressors.Xz.Filters;

namespace SharpCompress.Compressors.Xz;

public sealed partial class XZBlock
{
    public override async Task<int> ReadAsync(
        byte[] buffer,
        int offset,
        int count,
        CancellationToken cancellationToken = default
    )
    {
        var bytesRead = 0;
        if (!HeaderIsLoaded)
        {
            await LoadHeaderAsync(cancellationToken).ConfigureAwait(false);
        }

        if (!_streamConnected)
        {
            ConnectStream();
        }

        if (!_endOfStream)
        {
            bytesRead = await _decomStream
                .ReadAsync(buffer, offset, count, cancellationToken)
                .ConfigureAwait(false);
        }

        if (bytesRead != count)
        {
            _endOfStream = true;
        }

        if (_endOfStream && !_paddingSkipped)
        {
            await SkipPaddingAsync(cancellationToken).ConfigureAwait(false);
        }

        if (_endOfStream && !_crcChecked)
        {
            await CheckCrcAsync(cancellationToken).ConfigureAwait(false);
        }

        return bytesRead;
    }

    private async ValueTask SkipPaddingAsync(CancellationToken cancellationToken = default)
    {
        var bytes = (BaseStream.Position - _startPosition) % 4;
        if (bytes > 0)
        {
            var paddingBytes = new byte[4 - bytes];
            await BaseStream
                .ReadAsync(paddingBytes, 0, paddingBytes.Length, cancellationToken)
                .ConfigureAwait(false);
            if (paddingBytes.Any(b => b != 0))
            {
                throw new InvalidFormatException("Padding bytes were non-null");
            }
        }
        _paddingSkipped = true;
    }

    private async ValueTask CheckCrcAsync(CancellationToken cancellationToken = default)
    {
        var crc = new byte[_checkSize];
        await BaseStream.ReadAsync(crc, 0, _checkSize, cancellationToken).ConfigureAwait(false);
        // Actually do a check (and read in the bytes
        //   into the function throughout the stream read).
        _crcChecked = true;
    }

    private async ValueTask LoadHeaderAsync(CancellationToken cancellationToken = default)
    {
        await ReadHeaderSizeAsync(cancellationToken).ConfigureAwait(false);
        var headerCache = await CacheHeaderAsync(cancellationToken).ConfigureAwait(false);

        using (var cache = new MemoryStream(headerCache))
        using (var cachedReader = new BinaryReader(cache))
        {
            cachedReader.BaseStream.Position = 1; // skip the header size byte
            ReadBlockFlags(cachedReader);
            ReadFilters(cachedReader);
        }
        HeaderIsLoaded = true;
    }

    private async ValueTask ReadHeaderSizeAsync(CancellationToken cancellationToken = default)
    {
        var buffer = new byte[1];
        await BaseStream.ReadAsync(buffer, 0, 1, cancellationToken).ConfigureAwait(false);
        _blockHeaderSizeByte = buffer[0];
        if (_blockHeaderSizeByte == 0)
        {
            throw new XZIndexMarkerReachedException();
        }
    }

    private async ValueTask<byte[]> CacheHeaderAsync(CancellationToken cancellationToken = default)
    {
        var blockHeaderWithoutCrc = new byte[BlockHeaderSize - 4];
        blockHeaderWithoutCrc[0] = _blockHeaderSizeByte;
        var read = await BaseStream
            .ReadAsync(blockHeaderWithoutCrc, 1, BlockHeaderSize - 5, cancellationToken)
            .ConfigureAwait(false);
        if (read != BlockHeaderSize - 5)
        {
            throw new EndOfStreamException("Reached end of stream unexpectedly");
        }

        var crc = await BaseStream
            .ReadLittleEndianUInt32Async(cancellationToken)
            .ConfigureAwait(false);
        var calcCrc = Crc32.Compute(blockHeaderWithoutCrc);
        if (crc != calcCrc)
        {
            throw new InvalidFormatException("Block header corrupt");
        }

        return blockHeaderWithoutCrc;
    }
}
