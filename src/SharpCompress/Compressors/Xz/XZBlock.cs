using System;
using System.Buffers;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using SharpCompress.Compressors.Xz.Filters;

namespace SharpCompress.Compressors.Xz
{
    internal sealed class XZBlock : XZReadOnlyStream
    {
        public int BlockHeaderSize => (_blockHeaderSizeByte + 1) * 4;
        public ulong? CompressedSize { get; private set; }
        public ulong? UncompressedSize { get; private set; }
        public Stack<BlockFilter> Filters { get; private set; } = new Stack<BlockFilter>();
        public bool HeaderIsLoaded { get; private set; }
        private CheckType _checkType;
        private readonly int _checkSize;
        private bool _streamConnected;
        private int _numFilters;
        private byte _blockHeaderSizeByte;
        private Stream? _decomStream;
        private bool _endOfStream;
        private bool _paddingSkipped;
        private bool _crcChecked;
        private ulong _bytesRead;

        public XZBlock(Stream stream, CheckType checkType, int checkSize)
            : base(stream)
        {
            _checkType = checkType;
            _checkSize = checkSize;
        }

        public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            int bytesRead = 0;
            if (!HeaderIsLoaded)
            {
                await LoadHeader(cancellationToken);
            }

            if (!_streamConnected)
            {
                await ConnectStreamAsync();
            }

            if (!_endOfStream && _decomStream is not null)
            {
                bytesRead = await _decomStream.ReadAsync(buffer, cancellationToken);
            }

            if (bytesRead != buffer.Length)
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

            _bytesRead += (ulong)bytesRead;
            return bytesRead;
        }

        private void SkipPadding()
        {
            int bytes = (int)(BaseStream.Position % 4);
            if (bytes > 0)
            {
                byte[] paddingBytes = new byte[4 - bytes];
                BaseStream.Read(paddingBytes, 0, paddingBytes.Length);
                if (paddingBytes.Any(b => b != 0))
                {
                    throw new InvalidDataException("Padding bytes were non-null");
                }
            }
            _paddingSkipped = true;
        }

        private void CheckCrc()
        {
            byte[] crc = new byte[_checkSize];
            BaseStream.Read(crc, 0, _checkSize);
            // Actually do a check (and read in the bytes
            //   into the function throughout the stream read).
            _crcChecked = true;
        }

        private async ValueTask ConnectStreamAsync()
        {
            _decomStream = BaseStream;
            while (Filters.Any())
            {
                BlockFilter filter = Filters.Pop();
                await filter.SetBaseStreamAsync(_decomStream);
                _decomStream = filter;
            }
            _streamConnected = true;
        }

        private async ValueTask LoadHeader(CancellationToken cancellationToken)
        {
            await ReadHeaderSize(cancellationToken);
            using var blockHeaderWithoutCrc = MemoryPool<byte>.Shared.Rent(BlockHeaderSize - 4);
            var headerCache = blockHeaderWithoutCrc.Memory.Slice(0, BlockHeaderSize - 4);
            await CacheHeader(headerCache, cancellationToken);

            //TODO: memory-size this
            await using (var cache = new MemoryStream(headerCache.ToArray()))
            using (var cachedReader = new BinaryReader(cache))
            {
                cachedReader.BaseStream.Position = 1; // skip the header size byte
                ReadBlockFlags(cachedReader);
                ReadFilters(cachedReader);
            }
            HeaderIsLoaded = true;
        }

        private async ValueTask ReadHeaderSize(CancellationToken cancellationToken)
        {
            using var buffer = MemoryPool<byte>.Shared.Rent(1);
            await BaseStream.ReadAsync(buffer.Memory.Slice(0, 1), cancellationToken);
            _blockHeaderSizeByte = buffer.Memory.Span[0];
            if (_blockHeaderSizeByte == 0)
            {
                throw new XZIndexMarkerReachedException();
            }
        }

        private async ValueTask CacheHeader(Memory<byte> blockHeaderWithoutCrc, CancellationToken cancellationToken)
        {
            blockHeaderWithoutCrc.Span[0] = _blockHeaderSizeByte;
            var read = await BaseStream.ReadAsync(blockHeaderWithoutCrc.Slice( 1, BlockHeaderSize - 5), cancellationToken);
            if (read != BlockHeaderSize - 5)
            {
                throw new EndOfStreamException("Reached end of stream unexectedly");
            }

            uint crc = await BaseStream.ReadLittleEndianUInt32(cancellationToken);
            uint calcCrc = Crc32.Compute(blockHeaderWithoutCrc);
            if (crc != calcCrc)
            {
                throw new InvalidDataException("Block header corrupt");
            }
        }

        private void ReadBlockFlags(BinaryReader reader)
        {
            var blockFlags = reader.ReadByte();
            _numFilters = (blockFlags & 0x03) + 1;
            byte reserved = (byte)(blockFlags & 0x3C);

            if (reserved != 0)
            {
                throw new InvalidDataException("Reserved bytes used, perhaps an unknown XZ implementation");
            }

            bool compressedSizePresent = (blockFlags & 0x40) != 0;
            bool uncompressedSizePresent = (blockFlags & 0x80) != 0;

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
            int nonLastSizeChangers = 0;
            for (int i = 0; i < _numFilters; i++)
            {
                var filter = BlockFilter.Read(reader);
                if ((i + 1 == _numFilters && !filter.AllowAsLast)
                    || (i + 1 < _numFilters && !filter.AllowAsNonLast))
                {
                    throw new InvalidDataException("Block Filters in bad order");
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
                throw new InvalidDataException("More than two non-last block filters cannot change stream size");
            }

            int blockHeaderPaddingSize = BlockHeaderSize -
                                         (4 + (int)(reader.BaseStream.Position - baseStreamOffset));
            byte[] blockHeaderPadding = reader.ReadBytes(blockHeaderPaddingSize);
            if (!blockHeaderPadding.All(b => b == 0))
            {
                throw new InvalidDataException("Block header contains unknown fields");
            }
        }
    }
}
