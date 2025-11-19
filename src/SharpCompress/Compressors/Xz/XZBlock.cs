#nullable disable

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using SharpCompress.Common;
using SharpCompress.Compressors.Xz.Filters;

namespace SharpCompress.Compressors.Xz;

[CLSCompliant(false)]
public sealed class XZBlock : XZReadOnlyStream
{
    public int BlockHeaderSize => (_blockHeaderSizeByte + 1) * 4;
    public ulong? CompressedSize { get; private set; }
    public ulong? UncompressedSize { get; private set; }
    public Stack<BlockFilter> Filters { get; private set; } = new();
    public bool HeaderIsLoaded { get; private set; }
    private CheckType _checkType;
    private readonly int _checkSize;
    private bool _streamConnected;
    private int _numFilters;
    private byte _blockHeaderSizeByte;
    private Stream _decomStream;
    private bool _endOfStream;
    private bool _paddingSkipped;
    private bool _crcChecked;
    private readonly long _startPosition;

    public XZBlock(Stream stream, CheckType checkType, int checkSize)
        : base(stream)
    {
        _checkType = checkType;
        _checkSize = checkSize;
        _startPosition = stream.Position;
    }

    public override int Read(byte[] buffer, int offset, int count)
    {
        var bytesRead = 0;
        if (!HeaderIsLoaded)
        {
            LoadHeader();
        }

        if (!_streamConnected)
        {
            ConnectStream();
        }

        if (!_endOfStream)
        {
            bytesRead = _decomStream.Read(buffer, offset, count);
        }

        if (bytesRead != count)
        {
            _endOfStream = true;
        }

        if (_endOfStream && !_paddingSkipped)
        {
            SkipPadding();
        }

        if (_endOfStream && !_crcChecked)
        {
            CheckCrc();
        }

        return bytesRead;
    }

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

    private void SkipPadding()
    {
        var bytes = (BaseStream.Position - _startPosition) % 4;
        if (bytes > 0)
        {
            var paddingBytes = new byte[4 - bytes];
            BaseStream.Read(paddingBytes, 0, paddingBytes.Length);
            if (paddingBytes.Any(b => b != 0))
            {
                throw new InvalidFormatException("Padding bytes were non-null");
            }
        }
        _paddingSkipped = true;
    }

    private async Task SkipPaddingAsync(CancellationToken cancellationToken = default)
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

    private void CheckCrc()
    {
        var crc = new byte[_checkSize];
        BaseStream.Read(crc, 0, _checkSize);
        // Actually do a check (and read in the bytes
        //   into the function throughout the stream read).
        _crcChecked = true;
    }

    private async Task CheckCrcAsync(CancellationToken cancellationToken = default)
    {
        var crc = new byte[_checkSize];
        await BaseStream.ReadAsync(crc, 0, _checkSize, cancellationToken).ConfigureAwait(false);
        // Actually do a check (and read in the bytes
        //   into the function throughout the stream read).
        _crcChecked = true;
    }

    private void ConnectStream()
    {
        _decomStream = BaseStream;
        while (Filters.Any())
        {
            var filter = Filters.Pop();
            filter.SetBaseStream(_decomStream);
            _decomStream = filter;
        }
        _streamConnected = true;
    }

    private void LoadHeader()
    {
        ReadHeaderSize();
        var headerCache = CacheHeader();

        using (var cache = new MemoryStream(headerCache))
        using (var cachedReader = new BinaryReader(cache))
        {
            cachedReader.BaseStream.Position = 1; // skip the header size byte
            ReadBlockFlags(cachedReader);
            ReadFilters(cachedReader);
        }
        HeaderIsLoaded = true;
    }

    private async Task LoadHeaderAsync(CancellationToken cancellationToken = default)
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

    private void ReadHeaderSize()
    {
        _blockHeaderSizeByte = (byte)BaseStream.ReadByte();
        if (_blockHeaderSizeByte == 0)
        {
            throw new XZIndexMarkerReachedException();
        }
    }

    private async Task ReadHeaderSizeAsync(CancellationToken cancellationToken = default)
    {
        var buffer = new byte[1];
        await BaseStream.ReadAsync(buffer, 0, 1, cancellationToken).ConfigureAwait(false);
        _blockHeaderSizeByte = buffer[0];
        if (_blockHeaderSizeByte == 0)
        {
            throw new XZIndexMarkerReachedException();
        }
    }

    private byte[] CacheHeader()
    {
        var blockHeaderWithoutCrc = new byte[BlockHeaderSize - 4];
        blockHeaderWithoutCrc[0] = _blockHeaderSizeByte;
        var read = BaseStream.Read(blockHeaderWithoutCrc, 1, BlockHeaderSize - 5);
        if (read != BlockHeaderSize - 5)
        {
            throw new EndOfStreamException("Reached end of stream unexpectedly");
        }

        var crc = BaseStream.ReadLittleEndianUInt32();
        var calcCrc = Crc32.Compute(blockHeaderWithoutCrc);
        if (crc != calcCrc)
        {
            throw new InvalidFormatException("Block header corrupt");
        }

        return blockHeaderWithoutCrc;
    }

    private async Task<byte[]> CacheHeaderAsync(CancellationToken cancellationToken = default)
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

    private void ReadBlockFlags(BinaryReader reader)
    {
        var blockFlags = reader.ReadByte();
        _numFilters = (blockFlags & 0x03) + 1;
        var reserved = (byte)(blockFlags & 0x3C);

        if (reserved != 0)
        {
            throw new InvalidFormatException(
                "Reserved bytes used, perhaps an unknown XZ implementation"
            );
        }

        var compressedSizePresent = (blockFlags & 0x40) != 0;
        var uncompressedSizePresent = (blockFlags & 0x80) != 0;

        if (compressedSizePresent)
        {
            CompressedSize = reader.ReadXZInteger();
        }

        if (uncompressedSizePresent)
        {
            UncompressedSize = reader.ReadXZInteger();
        }
    }

    private void ReadFilters(BinaryReader reader, long baseStreamOffset = 0)
    {
        var nonLastSizeChangers = 0;
        for (var i = 0; i < _numFilters; i++)
        {
            var filter = BlockFilter.Read(reader);
            if (
                (i + 1 == _numFilters && !filter.AllowAsLast)
                || (i + 1 < _numFilters && !filter.AllowAsNonLast)
            )
            {
                throw new InvalidFormatException("Block Filters in bad order");
            }

            if (filter.ChangesDataSize && i + 1 < _numFilters)
            {
                nonLastSizeChangers++;
            }

            filter.ValidateFilter();
            Filters.Push(filter);
        }
        if (nonLastSizeChangers > 2)
        {
            throw new InvalidFormatException(
                "More than two non-last block filters cannot change stream size"
            );
        }

        var blockHeaderPaddingSize =
            BlockHeaderSize - (4 + (int)(reader.BaseStream.Position - baseStreamOffset));
        var blockHeaderPadding = reader.ReadBytes(blockHeaderPaddingSize);
        if (!blockHeaderPadding.All(b => b == 0))
        {
            throw new InvalidFormatException("Block header contains unknown fields");
        }
    }
}
