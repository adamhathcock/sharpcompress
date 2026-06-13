using System.Buffers;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using SharpCompress.Common;

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
                .NotNull()
                .ReadAsync(buffer, offset, count, cancellationToken)
                .ConfigureAwait(false);
            UpdateCheck(buffer, offset, bytesRead);
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
            var size = 4 - (int)bytes;
            var paddingBytes = ArrayPool<byte>.Shared.Rent(size);
            try
            {
                await BaseStream
                    .ReadExactAsync(paddingBytes, 0, size, cancellationToken)
                    .ConfigureAwait(false);
                if (paddingBytes.Any(b => b != 0))
                {
                    throw new InvalidFormatException("Padding bytes were non-null");
                }
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(paddingBytes);
            }
        }
        _paddingSkipped = true;
    }

    private async ValueTask CheckCrcAsync(CancellationToken cancellationToken = default)
    {
        var crc = ArrayPool<byte>.Shared.Rent(_checkSize);
        try
        {
            await BaseStream
                .ReadExactAsync(crc, 0, _checkSize, cancellationToken)
                .ConfigureAwait(false);
            VerifyCheck(crc);
            _crcChecked = true;
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(crc);
        }
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
        var buffer = ArrayPool<byte>.Shared.Rent(1);
        try
        {
            await BaseStream.ReadExactAsync(buffer, 0, 1, cancellationToken).ConfigureAwait(false);
            _blockHeaderSizeByte = buffer[0];
            if (_blockHeaderSizeByte == 0)
            {
                throw new XZIndexMarkerReachedException();
            }
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
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
            throw new IncompleteArchiveException("Reached end of stream unexpectedly");
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
