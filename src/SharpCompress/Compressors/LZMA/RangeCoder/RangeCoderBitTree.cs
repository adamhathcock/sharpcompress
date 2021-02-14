using System;
using System.Threading;
using System.Threading.Tasks;

namespace SharpCompress.Compressors.LZMA.RangeCoder
{
    internal struct BitTreeEncoder
    {
        private readonly BitEncoder[] _models;
        private readonly int _numBitLevels;

        public BitTreeEncoder(int numBitLevels)
        {
            _numBitLevels = numBitLevels;
            _models = new BitEncoder[1 << numBitLevels];
        }

        public void Init()
        {
            for (uint i = 1; i < (1 << _numBitLevels); i++)
            {
                _models[i].Init();
            }
        }

        public async ValueTask EncodeAsync(Encoder rangeEncoder, UInt32 symbol)
        {
            UInt32 m = 1;
            for (int bitIndex = _numBitLevels; bitIndex > 0;)
            {
                bitIndex--;
                UInt32 bit = (symbol >> bitIndex) & 1;
                await _models[m].EncodeAsync(rangeEncoder, bit);
                m = (m << 1) | bit;
            }
        }

        public async ValueTask ReverseEncodeAsync(Encoder rangeEncoder, UInt32 symbol)
        {
            UInt32 m = 1;
            for (UInt32 i = 0; i < _numBitLevels; i++)
            {
                UInt32 bit = symbol & 1;
                await _models[m].EncodeAsync(rangeEncoder, bit);
                m = (m << 1) | bit;
                symbol >>= 1;
            }
        }

        public UInt32 GetPrice(UInt32 symbol)
        {
            UInt32 price = 0;
            UInt32 m = 1;
            for (int bitIndex = _numBitLevels; bitIndex > 0;)
            {
                bitIndex--;
                UInt32 bit = (symbol >> bitIndex) & 1;
                price += _models[m].GetPrice(bit);
                m = (m << 1) + bit;
            }
            return price;
        }

        public UInt32 ReverseGetPrice(UInt32 symbol)
        {
            UInt32 price = 0;
            UInt32 m = 1;
            for (int i = _numBitLevels; i > 0; i--)
            {
                UInt32 bit = symbol & 1;
                symbol >>= 1;
                price += _models[m].GetPrice(bit);
                m = (m << 1) | bit;
            }
            return price;
        }

        public static UInt32 ReverseGetPrice(BitEncoder[] models, UInt32 startIndex,
                                             int numBitLevels, UInt32 symbol)
        {
            UInt32 price = 0;
            UInt32 m = 1;
            for (int i = numBitLevels; i > 0; i--)
            {
                UInt32 bit = symbol & 1;
                symbol >>= 1;
                price += models[startIndex + m].GetPrice(bit);
                m = (m << 1) | bit;
            }
            return price;
        }

        public static async ValueTask ReverseEncodeAsync(BitEncoder[] models, UInt32 startIndex,
                                         Encoder rangeEncoder, int numBitLevels, UInt32 symbol)
        {
            UInt32 m = 1;
            for (int i = 0; i < numBitLevels; i++)
            {
                UInt32 bit = symbol & 1;
                await models[startIndex + m].EncodeAsync(rangeEncoder, bit);
                m = (m << 1) | bit;
                symbol >>= 1;
            }
        }
    }

    internal struct BitTreeDecoder
    {
        private readonly BitDecoder[] _models;
        private readonly int _numBitLevels;

        public BitTreeDecoder(int numBitLevels)
        {
            _numBitLevels = numBitLevels;
            _models = new BitDecoder[1 << numBitLevels];
        }

        public void Init()
        {
            for (uint i = 1; i < (1 << _numBitLevels); i++)
            {
                _models[i].Init();
            }
        }

        public async ValueTask<uint> DecodeAsync(Decoder rangeDecoder, CancellationToken cancellationToken)
        {
            uint m = 1;
            for (int bitIndex = _numBitLevels; bitIndex > 0; bitIndex--)
            {
                m = (m << 1) + await _models[m].DecodeAsync(rangeDecoder, cancellationToken);
            }
            return m - ((uint)1 << _numBitLevels);
        }

        public async ValueTask<uint> ReverseDecode(Decoder rangeDecoder, CancellationToken cancellationToken)
        {
            uint m = 1;
            uint symbol = 0;
            for (int bitIndex = 0; bitIndex < _numBitLevels; bitIndex++)
            {
                uint bit = await _models[m].DecodeAsync(rangeDecoder, cancellationToken);
                m <<= 1;
                m += bit;
                symbol |= (bit << bitIndex);
            }
            return symbol;
        }

        public static async ValueTask<uint> ReverseDecode(BitDecoder[] models, UInt32 startIndex,
                                                          Decoder rangeDecoder, int numBitLevels, CancellationToken cancellationToken)
        {
            uint m = 1;
            uint symbol = 0;
            for (int bitIndex = 0; bitIndex < numBitLevels; bitIndex++)
            {
                uint bit = await models[startIndex + m].DecodeAsync(rangeDecoder, cancellationToken);
                m <<= 1;
                m += bit;
                symbol |= (bit << bitIndex);
            }
            return symbol;
        }
    }
}