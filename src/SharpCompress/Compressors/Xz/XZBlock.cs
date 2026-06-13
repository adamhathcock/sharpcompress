using System;
using System.Buffers;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using SharpCompress.Common;
using SharpCompress.Compressors.Xz.Filters;

namespace SharpCompress.Compressors.Xz;

[CLSCompliant(false)]
public sealed partial class XZBlock : XZReadOnlyStream
{
    private int BlockHeaderSize => (_blockHeaderSizeByte + 1) * 4;
    public ulong? CompressedSize { get; private set; }
    public ulong? UncompressedSize { get; private set; }
    private readonly Stack<BlockFilter> _filters = new();
    private bool HeaderIsLoaded { get; set; }
    private readonly CheckType _checkType;
    private readonly int _checkSize;
    private uint _crc32 = Crc32.DefaultSeed;
    private ulong _crc64 = Crc64.XZ_SEED;
    private readonly SHA256? _sha256;
    private bool _streamConnected;
    private int _numFilters;
    private byte _blockHeaderSizeByte;
    private Stream? _decomStream;
    private bool _endOfStream;
    private bool _paddingSkipped;
    private bool _crcChecked;
    private readonly long _startPosition;

    public XZBlock(Stream stream, CheckType checkType, int checkSize)
        : base(stream)
    {
        _checkType = checkType;
        _checkSize = checkSize;
        if (checkType == CheckType.SHA256)
        {
            _sha256 = SHA256.Create();
        }
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
            bytesRead = _decomStream.NotNull().Read(buffer, offset, count);
            UpdateCheck(buffer, offset, bytesRead);
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

    private void CheckCrc()
    {
        var crc = ArrayPool<byte>.Shared.Rent(_checkSize);
        try
        {
            BaseStream.ReadExact(crc, 0, _checkSize);
            VerifyCheck(crc.AsSpan().Slice(0, _checkSize));
            _crcChecked = true;
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(crc);
        }
    }

    private void UpdateCheck(byte[] buffer, int offset, int count)
    {
        if (count == 0 || _checkType == CheckType.NONE)
        {
            return;
        }

        var bytes = buffer.AsSpan(offset, count);
        switch (_checkType)
        {
            case CheckType.CRC32:
                _crc32 = Crc32.Update(_crc32, bytes);
                break;
            case CheckType.CRC64:
                _crc64 = Crc64.UpdateXz(_crc64, bytes);
                break;
            case CheckType.SHA256:
                _sha256.NotNull().TransformBlock(buffer, offset, count, null, 0);
                break;
        }
    }

    private void VerifyCheck(ReadOnlySpan<byte> expected)
    {
        switch (_checkType)
        {
            case CheckType.NONE:
                break;
            case CheckType.CRC32:
                GetLittleEndianBytes(~_crc32, expected);
                break;
            case CheckType.CRC64:
                GetLittleEndianBytes(~_crc64, expected);
                break;
            case CheckType.SHA256:
                FinalizeSha256Check(expected);
                break;
            default:
                throw new InvalidFormatException("Unsupported XZ check type");
        }
    }

    private static void GetLittleEndianBytes(uint value, ReadOnlySpan<byte> expected)
    {
        var bytes = ArrayPool<byte>.Shared.Rent(sizeof(uint));
        try
        {
            BinaryPrimitives.WriteUInt32LittleEndian(bytes, value);
            if (!expected.SequenceEqual(bytes.AsSpan().Slice(0, sizeof(uint))))
            {
                throw new InvalidFormatException("Block check corrupt");
            }
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(bytes);
        }
    }

    private static void GetLittleEndianBytes(ulong value, ReadOnlySpan<byte> expected)
    {
        var bytes = ArrayPool<byte>.Shared.Rent(sizeof(ulong));
        try
        {
            BinaryPrimitives.WriteUInt64LittleEndian(bytes, value);
            if (!expected.SequenceEqual(bytes.AsSpan().Slice(0, sizeof(ulong))))
            {
                throw new InvalidFormatException("Block check corrupt");
            }
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(bytes);
        }
    }

    private void FinalizeSha256Check(ReadOnlySpan<byte> expected)
    {
        _sha256.NotNull().TransformFinalBlock(Array.Empty<byte>(), 0, 0);
        if (!expected.SequenceEqual(_sha256.NotNull().Hash))
        {
            throw new InvalidFormatException("Block check corrupt");
        }
    }

    private void ConnectStream()
    {
        _decomStream = BaseStream;
        while (_filters.Any())
        {
            var filter = _filters.Pop();
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

    private void ReadHeaderSize()
    {
        _blockHeaderSizeByte = (byte)BaseStream.ReadByte();
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
            throw new IncompleteArchiveException("Reached end of stream unexpectedly");
        }

        var crc = BaseStream.ReadLittleEndianUInt32();
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
            _filters.Push(filter);
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
