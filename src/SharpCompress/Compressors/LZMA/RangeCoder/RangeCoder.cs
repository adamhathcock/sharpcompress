#nullable disable

using System;
using System.Buffers;
using System.IO;
using System.Threading.Tasks;

namespace SharpCompress.Compressors.LZMA.RangeCoder
{
    internal class Encoder : IAsyncDisposable
    {
        public const uint K_TOP_VALUE = (1 << 24);

        private Stream _stream;

        public UInt64 _low;
        public uint _range;
        private uint _cacheSize;
        private byte _cache;

        //long StartPosition;

        public void SetStream(Stream stream)
        {
            _stream = stream;
        }

        public void ReleaseStream()
        {
            _stream = null;
        }

        public void Init()
        {
            //StartPosition = Stream.Position;

            _low = 0;
            _range = 0xFFFFFFFF;
            _cacheSize = 1;
            _cache = 0;
        }

        public async ValueTask FlushData()
        {
            for (int i = 0; i < 5; i++)
            {
                await ShiftLowAsync();
            }
        }

        public Task FlushAsync()
        {
            return _stream.FlushAsync();
        }

        public ValueTask DisposeAsync()
        {
            return _stream.DisposeAsync();
        }

        public async ValueTask EncodeAsync(uint start, uint size, uint total)
        {
            _low += start * (_range /= total);
            _range *= size;
            while (_range < K_TOP_VALUE)
            {
                _range <<= 8;
                await ShiftLowAsync();
            }
        }

        public async ValueTask ShiftLowAsync()
        {
            if ((uint)_low < 0xFF000000 || (uint)(_low >> 32) == 1)
            {
                using var buffer = MemoryPool<byte>.Shared.Rent(1);
                var b = buffer.Memory.Slice(0,1);
                byte temp = _cache;
                do
                {
                    b.Span[0] = (byte)(temp + (_low >> 32));
                    await _stream.WriteAsync(b);
                    temp = 0xFF;
                }
                while (--_cacheSize != 0);
                _cache = (byte)(((uint)_low) >> 24);
            }
            _cacheSize++;
            _low = ((uint)_low) << 8;
        }

        public async ValueTask EncodeDirectBits(uint v, int numTotalBits)
        {
            for (int i = numTotalBits - 1; i >= 0; i--)
            {
                _range >>= 1;
                if (((v >> i) & 1) == 1)
                {
                    _low += _range;
                }
                if (_range < K_TOP_VALUE)
                {
                    _range <<= 8;
                    await ShiftLowAsync();
                }
            }
        }

        public async ValueTask EncodeBitAsync(uint size0, int numTotalBits, uint symbol)
        {
            uint newBound = (_range >> numTotalBits) * size0;
            if (symbol == 0)
            {
                _range = newBound;
            }
            else
            {
                _low += newBound;
                _range -= newBound;
            }
            while (_range < K_TOP_VALUE)
            {
                _range <<= 8;
                await ShiftLowAsync();
            }
        }

        public long GetProcessedSizeAdd()
        {
            return -1;

            //return _cacheSize + Stream.Position - StartPosition + 4;
            // (long)Stream.GetProcessedSize();
        }
    }

    internal class Decoder: IAsyncDisposable
    {
        public const uint K_TOP_VALUE = (1 << 24);
        public uint _range;
        public uint _code;

        // public Buffer.InBuffer Stream = new Buffer.InBuffer(1 << 16);
        public Stream _stream;
        public long _total;

        public async ValueTask InitAsync(Stream stream)
        {
            // Stream.Init(stream);
            _stream = stream;

            _code = 0;
            _range = 0xFFFFFFFF;
            using var buffer = MemoryPool<byte>.Shared.Rent(1);
            var b = buffer.Memory.Slice(0,1);
            for (int i = 0; i < 5; i++)
            {
                await _stream.ReadAsync(b);
                _code = (_code << 8) | b.Span[0];
            }
            _total = 5;
        }

        public void ReleaseStream()
        {
            // Stream.ReleaseStream();
            _stream = null;
        }

        public ValueTask DisposeAsync()
        {
            return _stream.DisposeAsync();
        }

        public async ValueTask NormalizeAsync()
        {
            using var buffer = MemoryPool<byte>.Shared.Rent(1);
            var b = buffer.Memory.Slice(0,1);
            while (_range < K_TOP_VALUE)
            {
                await _stream.ReadAsync(b);
                _code = (_code << 8) | b.Span[0];
                _range <<= 8;
                _total++;
            }
        }
        
        public uint GetThreshold(uint total)
        {
            return _code / (_range /= total);
        }

        public async ValueTask Decode(uint start, uint size)
        {
            _code -= start * _range;
            _range *= size;
            await NormalizeAsync();
        }

        public uint DecodeDirectBits(int numTotalBits)
        {
            uint range = _range;
            uint code = _code;
            uint result = 0;
            for (int i = numTotalBits; i > 0; i--)
            {
                range >>= 1;
                /*
                result <<= 1;
                if (code >= range)
                {
                    code -= range;
                    result |= 1;
                }
                */
                uint t = (code - range) >> 31;
                code -= range & (t - 1);
                result = (result << 1) | (1 - t);

                if (range < K_TOP_VALUE)
                {
                    code = (code << 8) | (byte)_stream.ReadByte();
                    range <<= 8;
                    _total++;
                }
            }
            _range = range;
            _code = code;
            return result;
        }

        public async ValueTask<uint> DecodeBitAsync(uint size0, int numTotalBits)
        {
            uint newBound = (_range >> numTotalBits) * size0;
            uint symbol;
            if (_code < newBound)
            {
                symbol = 0;
                _range = newBound;
            }
            else
            {
                symbol = 1;
                _code -= newBound;
                _range -= newBound;
            }
            await NormalizeAsync();
            return symbol;
        }

        public bool IsFinished => _code == 0;

        // ulong GetProcessedSize() {return Stream.GetProcessedSize(); }
    }
}