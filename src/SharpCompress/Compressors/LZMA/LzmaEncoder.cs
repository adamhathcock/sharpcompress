#nullable disable

using System;
using System.IO;
using SharpCompress.Compressors.LZMA.LZ;
using SharpCompress.Compressors.LZMA.RangeCoder;

namespace SharpCompress.Compressors.LZMA;

internal class Encoder : ICoder, ISetCoderProperties, IWriteCoderProperties
{
    private enum EMatchFinderType
    {
        Bt2,
        Bt4,
    }

    private const uint K_IFINITY_PRICE = 0xFFFFFFF;

    private static readonly byte[] G_FAST_POS = new byte[1 << 11];

    static Encoder()
    {
        const byte kFastSlots = 22;
        var c = 2;
        G_FAST_POS[0] = 0;
        G_FAST_POS[1] = 1;
        for (byte slotFast = 2; slotFast < kFastSlots; slotFast++)
        {
            var k = ((uint)1 << ((slotFast >> 1) - 1));
            for (uint j = 0; j < k; j++, c++)
            {
                G_FAST_POS[c] = slotFast;
            }
        }
    }

    private static uint GetPosSlot(uint pos)
    {
        if (pos < (1 << 11))
        {
            return G_FAST_POS[pos];
        }
        if (pos < (1 << 21))
        {
            return (uint)(G_FAST_POS[pos >> 10] + 20);
        }
        return (uint)(G_FAST_POS[pos >> 20] + 40);
    }

    private static uint GetPosSlot2(uint pos)
    {
        if (pos < (1 << 17))
        {
            return (uint)(G_FAST_POS[pos >> 6] + 12);
        }
        if (pos < (1 << 27))
        {
            return (uint)(G_FAST_POS[pos >> 16] + 32);
        }
        return (uint)(G_FAST_POS[pos >> 26] + 52);
    }

    private Base.State _state = new();
    private byte _previousByte;
    private readonly uint[] _repDistances = new uint[Base.K_NUM_REP_DISTANCES];

    private void BaseInit()
    {
        _state.Init();
        _previousByte = 0;
        for (uint i = 0; i < Base.K_NUM_REP_DISTANCES; i++)
        {
            _repDistances[i] = 0;
        }
    }

    private const int K_DEFAULT_DICTIONARY_LOG_SIZE = 22;
    private const uint K_NUM_FAST_BYTES_DEFAULT = 0x20;

    private class LiteralEncoder
    {
        public struct Encoder2
        {
            private BitEncoder[] _encoders;

            public void Create() => _encoders = new BitEncoder[0x300];

            public void Init()
            {
                for (var i = 0; i < 0x300; i++)
                {
                    _encoders[i].Init();
                }
            }

            public void Encode(RangeCoder.Encoder rangeEncoder, byte symbol)
            {
                uint context = 1;
                for (var i = 7; i >= 0; i--)
                {
                    var bit = (uint)((symbol >> i) & 1);
                    _encoders[context].Encode(rangeEncoder, bit);
                    context = (context << 1) | bit;
                }
            }

            public void EncodeMatched(RangeCoder.Encoder rangeEncoder, byte matchByte, byte symbol)
            {
                uint context = 1;
                var same = true;
                for (var i = 7; i >= 0; i--)
                {
                    var bit = (uint)((symbol >> i) & 1);
                    var state = context;
                    if (same)
                    {
                        var matchBit = (uint)((matchByte >> i) & 1);
                        state += ((1 + matchBit) << 8);
                        same = (matchBit == bit);
                    }
                    _encoders[state].Encode(rangeEncoder, bit);
                    context = (context << 1) | bit;
                }
            }

            public uint GetPrice(bool matchMode, byte matchByte, byte symbol)
            {
                uint price = 0;
                uint context = 1;
                var i = 7;
                if (matchMode)
                {
                    for (; i >= 0; i--)
                    {
                        var matchBit = (uint)(matchByte >> i) & 1;
                        var bit = (uint)(symbol >> i) & 1;
                        price += _encoders[((1 + matchBit) << 8) + context].GetPrice(bit);
                        context = (context << 1) | bit;
                        if (matchBit != bit)
                        {
                            i--;
                            break;
                        }
                    }
                }
                for (; i >= 0; i--)
                {
                    var bit = (uint)(symbol >> i) & 1;
                    price += _encoders[context].GetPrice(bit);
                    context = (context << 1) | bit;
                }
                return price;
            }
        }

        private Encoder2[] _coders;
        private int _numPrevBits;
        private int _numPosBits;
        private uint _posMask;

        public void Create(int numPosBits, int numPrevBits)
        {
            if (_coders != null && _numPrevBits == numPrevBits && _numPosBits == numPosBits)
            {
                return;
            }
            _numPosBits = numPosBits;
            _posMask = ((uint)1 << numPosBits) - 1;
            _numPrevBits = numPrevBits;
            var numStates = (uint)1 << (_numPrevBits + _numPosBits);
            _coders = new Encoder2[numStates];
            for (uint i = 0; i < numStates; i++)
            {
                _coders[i].Create();
            }
        }

        public void Init()
        {
            var numStates = (uint)1 << (_numPrevBits + _numPosBits);
            for (uint i = 0; i < numStates; i++)
            {
                _coders[i].Init();
            }
        }

        public Encoder2 GetSubCoder(uint pos, byte prevByte) =>
            _coders[((pos & _posMask) << _numPrevBits) + (uint)(prevByte >> (8 - _numPrevBits))];
    }

    private class LenEncoder
    {
        private BitEncoder _choice = new();
        private BitEncoder _choice2 = new();
        private readonly BitTreeEncoder[] _lowCoder = new BitTreeEncoder[
            Base.K_NUM_POS_STATES_ENCODING_MAX
        ];
        private readonly BitTreeEncoder[] _midCoder = new BitTreeEncoder[
            Base.K_NUM_POS_STATES_ENCODING_MAX
        ];
        private BitTreeEncoder _highCoder = new(Base.K_NUM_HIGH_LEN_BITS);

        public LenEncoder()
        {
            for (uint posState = 0; posState < Base.K_NUM_POS_STATES_ENCODING_MAX; posState++)
            {
                _lowCoder[posState] = new BitTreeEncoder(Base.K_NUM_LOW_LEN_BITS);
                _midCoder[posState] = new BitTreeEncoder(Base.K_NUM_MID_LEN_BITS);
            }
        }

        public void Init(uint numPosStates)
        {
            _choice.Init();
            _choice2.Init();
            for (uint posState = 0; posState < numPosStates; posState++)
            {
                _lowCoder[posState].Init();
                _midCoder[posState].Init();
            }
            _highCoder.Init();
        }

        public void Encode(RangeCoder.Encoder rangeEncoder, uint symbol, uint posState)
        {
            if (symbol < Base.K_NUM_LOW_LEN_SYMBOLS)
            {
                _choice.Encode(rangeEncoder, 0);
                _lowCoder[posState].Encode(rangeEncoder, symbol);
            }
            else
            {
                symbol -= Base.K_NUM_LOW_LEN_SYMBOLS;
                _choice.Encode(rangeEncoder, 1);
                if (symbol < Base.K_NUM_MID_LEN_SYMBOLS)
                {
                    _choice2.Encode(rangeEncoder, 0);
                    _midCoder[posState].Encode(rangeEncoder, symbol);
                }
                else
                {
                    _choice2.Encode(rangeEncoder, 1);
                    _highCoder.Encode(rangeEncoder, symbol - Base.K_NUM_MID_LEN_SYMBOLS);
                }
            }
        }

        public void SetPrices(uint posState, uint numSymbols, uint[] prices, uint st)
        {
            var a0 = _choice.GetPrice0();
            var a1 = _choice.GetPrice1();
            var b0 = a1 + _choice2.GetPrice0();
            var b1 = a1 + _choice2.GetPrice1();
            uint i = 0;
            for (i = 0; i < Base.K_NUM_LOW_LEN_SYMBOLS; i++)
            {
                if (i >= numSymbols)
                {
                    return;
                }
                prices[st + i] = a0 + _lowCoder[posState].GetPrice(i);
            }
            for (; i < Base.K_NUM_LOW_LEN_SYMBOLS + Base.K_NUM_MID_LEN_SYMBOLS; i++)
            {
                if (i >= numSymbols)
                {
                    return;
                }
                prices[st + i] = b0 + _midCoder[posState].GetPrice(i - Base.K_NUM_LOW_LEN_SYMBOLS);
            }
            for (; i < numSymbols; i++)
            {
                prices[st + i] =
                    b1
                    + _highCoder.GetPrice(
                        i - Base.K_NUM_LOW_LEN_SYMBOLS - Base.K_NUM_MID_LEN_SYMBOLS
                    );
            }
        }
    }

    private const uint K_NUM_LEN_SPEC_SYMBOLS =
        Base.K_NUM_LOW_LEN_SYMBOLS + Base.K_NUM_MID_LEN_SYMBOLS;

    private class LenPriceTableEncoder : LenEncoder
    {
        private readonly uint[] _prices = new uint[
            Base.K_NUM_LEN_SYMBOLS << Base.K_NUM_POS_STATES_BITS_ENCODING_MAX
        ];
        private uint _tableSize;
        private readonly uint[] _counters = new uint[Base.K_NUM_POS_STATES_ENCODING_MAX];

        public void SetTableSize(uint tableSize) => _tableSize = tableSize;

        public uint GetPrice(uint symbol, uint posState) =>
            _prices[(posState * Base.K_NUM_LEN_SYMBOLS) + symbol];

        private void UpdateTable(uint posState)
        {
            SetPrices(posState, _tableSize, _prices, posState * Base.K_NUM_LEN_SYMBOLS);
            _counters[posState] = _tableSize;
        }

        public void UpdateTables(uint numPosStates)
        {
            for (uint posState = 0; posState < numPosStates; posState++)
            {
                UpdateTable(posState);
            }
        }

        public new void Encode(RangeCoder.Encoder rangeEncoder, uint symbol, uint posState)
        {
            base.Encode(rangeEncoder, symbol, posState);
            if (--_counters[posState] == 0)
            {
                UpdateTable(posState);
            }
        }
    }

    private const uint K_NUM_OPTS = 1 << 12;

    private class Optimal
    {
        public Base.State _state;

        public bool _prev1IsChar;
        public bool _prev2;

        public uint _posPrev2;
        public uint _backPrev2;

        public uint _price;
        public uint _posPrev;
        public uint _backPrev;

        public uint _backs0;
        public uint _backs1;
        public uint _backs2;
        public uint _backs3;

        public void MakeAsChar()
        {
            _backPrev = 0xFFFFFFFF;
            _prev1IsChar = false;
        }

        public void MakeAsShortRep()
        {
            _backPrev = 0;
            ;
            _prev1IsChar = false;
        }

        public bool IsShortRep() => (_backPrev == 0);
    }

    private readonly Optimal[] _optimum = new Optimal[K_NUM_OPTS];
    private BinTree _matchFinder;
    private readonly RangeCoder.Encoder _rangeEncoder = new();

    private readonly BitEncoder[] _isMatch = new BitEncoder[
        Base.K_NUM_STATES << Base.K_NUM_POS_STATES_BITS_MAX
    ];

    private readonly BitEncoder[] _isRep = new BitEncoder[Base.K_NUM_STATES];
    private readonly BitEncoder[] _isRepG0 = new BitEncoder[Base.K_NUM_STATES];
    private readonly BitEncoder[] _isRepG1 = new BitEncoder[Base.K_NUM_STATES];
    private readonly BitEncoder[] _isRepG2 = new BitEncoder[Base.K_NUM_STATES];

    private readonly BitEncoder[] _isRep0Long = new BitEncoder[
        Base.K_NUM_STATES << Base.K_NUM_POS_STATES_BITS_MAX
    ];

    private readonly BitTreeEncoder[] _posSlotEncoder = new BitTreeEncoder[
        Base.K_NUM_LEN_TO_POS_STATES
    ];

    private readonly BitEncoder[] _posEncoders = new BitEncoder[
        Base.K_NUM_FULL_DISTANCES - Base.K_END_POS_MODEL_INDEX
    ];

    private BitTreeEncoder _posAlignEncoder = new(Base.K_NUM_ALIGN_BITS);

    private readonly LenPriceTableEncoder _lenEncoder = new();
    private readonly LenPriceTableEncoder _repMatchLenEncoder = new();

    private readonly LiteralEncoder _literalEncoder = new();

    private readonly uint[] _matchDistances = new uint[(Base.K_MATCH_MAX_LEN * 2) + 2];

    private uint _numFastBytes = K_NUM_FAST_BYTES_DEFAULT;
    private uint _longestMatchLength;
    private uint _numDistancePairs;

    private uint _additionalOffset;

    private uint _optimumEndIndex;
    private uint _optimumCurrentIndex;

    private bool _longestMatchWasFound;

    private readonly uint[] _posSlotPrices = new uint[
        1 << (Base.K_NUM_POS_SLOT_BITS + Base.K_NUM_LEN_TO_POS_STATES_BITS)
    ];
    private readonly uint[] _distancesPrices = new uint[
        Base.K_NUM_FULL_DISTANCES << Base.K_NUM_LEN_TO_POS_STATES_BITS
    ];
    private readonly uint[] _alignPrices = new uint[Base.K_ALIGN_TABLE_SIZE];
    private uint _alignPriceCount;

    private uint _distTableSize = (K_DEFAULT_DICTIONARY_LOG_SIZE * 2);

    private int _posStateBits = 2;
    private uint _posStateMask = (4 - 1);
    private int _numLiteralPosStateBits;
    private int _numLiteralContextBits = 3;

    private uint _dictionarySize = (1 << K_DEFAULT_DICTIONARY_LOG_SIZE);
    private uint _dictionarySizePrev = 0xFFFFFFFF;
    private uint _numFastBytesPrev = 0xFFFFFFFF;

    private long _nowPos64;
    private bool _finished;
    private Stream _inStream;

    private EMatchFinderType _matchFinderType = EMatchFinderType.Bt4;
    private bool _writeEndMark;

    private bool _needReleaseMfStream;
    private bool _processingMode;

    private void Create()
    {
        if (_matchFinder is null)
        {
            var bt = new BinTree();
            var numHashBytes = 4;
            if (_matchFinderType == EMatchFinderType.Bt2)
            {
                numHashBytes = 2;
            }
            bt.SetType(numHashBytes);
            _matchFinder = bt;
        }
        _literalEncoder.Create(_numLiteralPosStateBits, _numLiteralContextBits);

        if (_dictionarySize == _dictionarySizePrev && _numFastBytesPrev == _numFastBytes)
        {
            return;
        }
        _matchFinder.Create(
            _dictionarySize,
            K_NUM_OPTS,
            _numFastBytes,
            Base.K_MATCH_MAX_LEN + 1 + K_NUM_OPTS
        );
        _dictionarySizePrev = _dictionarySize;
        _numFastBytesPrev = _numFastBytes;
    }

    public Encoder()
    {
        for (var i = 0; i < K_NUM_OPTS; i++)
        {
            _optimum[i] = new Optimal();
        }
        for (var i = 0; i < Base.K_NUM_LEN_TO_POS_STATES; i++)
        {
            _posSlotEncoder[i] = new BitTreeEncoder(Base.K_NUM_POS_SLOT_BITS);
        }
    }

    private void SetWriteEndMarkerMode(bool writeEndMarker) => _writeEndMark = writeEndMarker;

    private void Init()
    {
        BaseInit();
        _rangeEncoder.Init();

        uint i;
        for (i = 0; i < Base.K_NUM_STATES; i++)
        {
            for (uint j = 0; j <= _posStateMask; j++)
            {
                var complexState = (i << Base.K_NUM_POS_STATES_BITS_MAX) + j;
                _isMatch[complexState].Init();
                _isRep0Long[complexState].Init();
            }
            _isRep[i].Init();
            _isRepG0[i].Init();
            _isRepG1[i].Init();
            _isRepG2[i].Init();
        }
        _literalEncoder.Init();
        for (i = 0; i < Base.K_NUM_LEN_TO_POS_STATES; i++)
        {
            _posSlotEncoder[i].Init();
        }
        for (i = 0; i < Base.K_NUM_FULL_DISTANCES - Base.K_END_POS_MODEL_INDEX; i++)
        {
            _posEncoders[i].Init();
        }

        _lenEncoder.Init((uint)1 << _posStateBits);
        _repMatchLenEncoder.Init((uint)1 << _posStateBits);

        _posAlignEncoder.Init();

        _longestMatchWasFound = false;
        _optimumEndIndex = 0;
        _optimumCurrentIndex = 0;
        _additionalOffset = 0;
    }

    private void ReadMatchDistances(out uint lenRes, out uint numDistancePairs)
    {
        lenRes = 0;
        numDistancePairs = _matchFinder.GetMatches(_matchDistances);
        if (numDistancePairs > 0)
        {
            lenRes = _matchDistances[numDistancePairs - 2];
            if (lenRes == _numFastBytes)
            {
                lenRes += _matchFinder.GetMatchLen(
                    (int)lenRes - 1,
                    _matchDistances[numDistancePairs - 1],
                    Base.K_MATCH_MAX_LEN - lenRes
                );
            }
        }
        _additionalOffset++;
    }

    private void MovePos(uint num)
    {
        if (num > 0)
        {
            _matchFinder.Skip(num);
            _additionalOffset += num;
        }
    }

    private uint GetRepLen1Price(Base.State state, uint posState) =>
        _isRepG0[state._index].GetPrice0()
        + _isRep0Long[(state._index << Base.K_NUM_POS_STATES_BITS_MAX) + posState].GetPrice0();

    private uint GetPureRepPrice(uint repIndex, Base.State state, uint posState)
    {
        uint price;
        if (repIndex == 0)
        {
            price = _isRepG0[state._index].GetPrice0();
            price += _isRep0Long[(state._index << Base.K_NUM_POS_STATES_BITS_MAX) + posState]
                .GetPrice1();
        }
        else
        {
            price = _isRepG0[state._index].GetPrice1();
            if (repIndex == 1)
            {
                price += _isRepG1[state._index].GetPrice0();
            }
            else
            {
                price += _isRepG1[state._index].GetPrice1();
                price += _isRepG2[state._index].GetPrice(repIndex - 2);
            }
        }
        return price;
    }

    private uint GetRepPrice(uint repIndex, uint len, Base.State state, uint posState)
    {
        var price = _repMatchLenEncoder.GetPrice(len - Base.K_MATCH_MIN_LEN, posState);
        return price + GetPureRepPrice(repIndex, state, posState);
    }

    private uint GetPosLenPrice(uint pos, uint len, uint posState)
    {
        uint price;
        var lenToPosState = Base.GetLenToPosState(len);
        if (pos < Base.K_NUM_FULL_DISTANCES)
        {
            price = _distancesPrices[(lenToPosState * Base.K_NUM_FULL_DISTANCES) + pos];
        }
        else
        {
            price =
                _posSlotPrices[(lenToPosState << Base.K_NUM_POS_SLOT_BITS) + GetPosSlot2(pos)]
                + _alignPrices[pos & Base.K_ALIGN_MASK];
        }
        return price + _lenEncoder.GetPrice(len - Base.K_MATCH_MIN_LEN, posState);
    }

    private uint Backward(out uint backRes, uint cur)
    {
        _optimumEndIndex = cur;
        var posMem = _optimum[cur]._posPrev;
        var backMem = _optimum[cur]._backPrev;
        do
        {
            if (_optimum[cur]._prev1IsChar)
            {
                _optimum[posMem].MakeAsChar();
                _optimum[posMem]._posPrev = posMem - 1;
                if (_optimum[cur]._prev2)
                {
                    _optimum[posMem - 1]._prev1IsChar = false;
                    _optimum[posMem - 1]._posPrev = _optimum[cur]._posPrev2;
                    _optimum[posMem - 1]._backPrev = _optimum[cur]._backPrev2;
                }
            }
            var posPrev = posMem;
            var backCur = backMem;

            backMem = _optimum[posPrev]._backPrev;
            posMem = _optimum[posPrev]._posPrev;

            _optimum[posPrev]._backPrev = backCur;
            _optimum[posPrev]._posPrev = cur;
            cur = posPrev;
        } while (cur > 0);
        backRes = _optimum[0]._backPrev;
        _optimumCurrentIndex = _optimum[0]._posPrev;
        return _optimumCurrentIndex;
    }

    private readonly uint[] _reps = new uint[Base.K_NUM_REP_DISTANCES];
    private readonly uint[] _repLens = new uint[Base.K_NUM_REP_DISTANCES];

    private uint GetOptimum(uint position, out uint backRes)
    {
        if (_optimumEndIndex != _optimumCurrentIndex)
        {
            var lenRes = _optimum[_optimumCurrentIndex]._posPrev - _optimumCurrentIndex;
            backRes = _optimum[_optimumCurrentIndex]._backPrev;
            _optimumCurrentIndex = _optimum[_optimumCurrentIndex]._posPrev;
            return lenRes;
        }
        _optimumCurrentIndex = _optimumEndIndex = 0;

        uint lenMain,
            numDistancePairs;
        if (!_longestMatchWasFound)
        {
            ReadMatchDistances(out lenMain, out numDistancePairs);
        }
        else
        {
            lenMain = _longestMatchLength;
            numDistancePairs = _numDistancePairs;
            _longestMatchWasFound = false;
        }

        var numAvailableBytes = _matchFinder.GetNumAvailableBytes() + 1;
        if (numAvailableBytes < 2)
        {
            backRes = 0xFFFFFFFF;
            return 1;
        }
        if (numAvailableBytes > Base.K_MATCH_MAX_LEN)
        {
            numAvailableBytes = Base.K_MATCH_MAX_LEN;
        }

        uint repMaxIndex = 0;
        uint i;
        for (i = 0; i < Base.K_NUM_REP_DISTANCES; i++)
        {
            _reps[i] = _repDistances[i];
            _repLens[i] = _matchFinder.GetMatchLen(0 - 1, _reps[i], Base.K_MATCH_MAX_LEN);
            if (_repLens[i] > _repLens[repMaxIndex])
            {
                repMaxIndex = i;
            }
        }
        if (_repLens[repMaxIndex] >= _numFastBytes)
        {
            backRes = repMaxIndex;
            var lenRes = _repLens[repMaxIndex];
            MovePos(lenRes - 1);
            return lenRes;
        }

        if (lenMain >= _numFastBytes)
        {
            backRes = _matchDistances[numDistancePairs - 1] + Base.K_NUM_REP_DISTANCES;
            MovePos(lenMain - 1);
            return lenMain;
        }

        var currentByte = _matchFinder.GetIndexByte(0 - 1);
        var matchByte = _matchFinder.GetIndexByte((int)(0 - _repDistances[0] - 1 - 1));

        if (lenMain < 2 && currentByte != matchByte && _repLens[repMaxIndex] < 2)
        {
            backRes = 0xFFFFFFFF;
            return 1;
        }

        _optimum[0]._state = _state;

        var posState = (position & _posStateMask);

        _optimum[1]._price =
            _isMatch[(_state._index << Base.K_NUM_POS_STATES_BITS_MAX) + posState].GetPrice0()
            + _literalEncoder
                .GetSubCoder(position, _previousByte)
                .GetPrice(!_state.IsCharState(), matchByte, currentByte);
        _optimum[1].MakeAsChar();

        var matchPrice = _isMatch[(_state._index << Base.K_NUM_POS_STATES_BITS_MAX) + posState]
            .GetPrice1();
        var repMatchPrice = matchPrice + _isRep[_state._index].GetPrice1();

        if (matchByte == currentByte)
        {
            var shortRepPrice = repMatchPrice + GetRepLen1Price(_state, posState);
            if (shortRepPrice < _optimum[1]._price)
            {
                _optimum[1]._price = shortRepPrice;
                _optimum[1].MakeAsShortRep();
            }
        }

        var lenEnd = ((lenMain >= _repLens[repMaxIndex]) ? lenMain : _repLens[repMaxIndex]);

        if (lenEnd < 2)
        {
            backRes = _optimum[1]._backPrev;
            return 1;
        }

        _optimum[1]._posPrev = 0;

        _optimum[0]._backs0 = _reps[0];
        _optimum[0]._backs1 = _reps[1];
        _optimum[0]._backs2 = _reps[2];
        _optimum[0]._backs3 = _reps[3];

        var len = lenEnd;
        do
        {
            _optimum[len--]._price = K_IFINITY_PRICE;
        } while (len >= 2);

        for (i = 0; i < Base.K_NUM_REP_DISTANCES; i++)
        {
            var repLen = _repLens[i];
            if (repLen < 2)
            {
                continue;
            }
            var price = repMatchPrice + GetPureRepPrice(i, _state, posState);
            do
            {
                var curAndLenPrice = price + _repMatchLenEncoder.GetPrice(repLen - 2, posState);
                var optimum = _optimum[repLen];
                if (curAndLenPrice < optimum._price)
                {
                    optimum._price = curAndLenPrice;
                    optimum._posPrev = 0;
                    optimum._backPrev = i;
                    optimum._prev1IsChar = false;
                }
            } while (--repLen >= 2);
        }

        var normalMatchPrice = matchPrice + _isRep[_state._index].GetPrice0();

        len = ((_repLens[0] >= 2) ? _repLens[0] + 1 : 2);
        if (len <= lenMain)
        {
            uint offs = 0;
            while (len > _matchDistances[offs])
            {
                offs += 2;
            }
            for (; ; len++)
            {
                var distance = _matchDistances[offs + 1];
                var curAndLenPrice = normalMatchPrice + GetPosLenPrice(distance, len, posState);
                var optimum = _optimum[len];
                if (curAndLenPrice < optimum._price)
                {
                    optimum._price = curAndLenPrice;
                    optimum._posPrev = 0;
                    optimum._backPrev = distance + Base.K_NUM_REP_DISTANCES;
                    optimum._prev1IsChar = false;
                }
                if (len == _matchDistances[offs])
                {
                    offs += 2;
                    if (offs == numDistancePairs)
                    {
                        break;
                    }
                }
            }
        }

        uint cur = 0;

        while (true)
        {
            cur++;
            if (cur == lenEnd)
            {
                return Backward(out backRes, cur);
            }
            ReadMatchDistances(out var newLen, out numDistancePairs);
            if (newLen >= _numFastBytes)
            {
                _numDistancePairs = numDistancePairs;
                _longestMatchLength = newLen;
                _longestMatchWasFound = true;
                return Backward(out backRes, cur);
            }
            position++;
            var posPrev = _optimum[cur]._posPrev;
            Base.State state;
            if (_optimum[cur]._prev1IsChar)
            {
                posPrev--;
                if (_optimum[cur]._prev2)
                {
                    state = _optimum[_optimum[cur]._posPrev2]._state;
                    if (_optimum[cur]._backPrev2 < Base.K_NUM_REP_DISTANCES)
                    {
                        state.UpdateRep();
                    }
                    else
                    {
                        state.UpdateMatch();
                    }
                }
                else
                {
                    state = _optimum[posPrev]._state;
                }
                state.UpdateChar();
            }
            else
            {
                state = _optimum[posPrev]._state;
            }
            if (posPrev == cur - 1)
            {
                if (_optimum[cur].IsShortRep())
                {
                    state.UpdateShortRep();
                }
                else
                {
                    state.UpdateChar();
                }
            }
            else
            {
                uint pos;
                if (_optimum[cur]._prev1IsChar && _optimum[cur]._prev2)
                {
                    posPrev = _optimum[cur]._posPrev2;
                    pos = _optimum[cur]._backPrev2;
                    state.UpdateRep();
                }
                else
                {
                    pos = _optimum[cur]._backPrev;
                    if (pos < Base.K_NUM_REP_DISTANCES)
                    {
                        state.UpdateRep();
                    }
                    else
                    {
                        state.UpdateMatch();
                    }
                }
                var opt = _optimum[posPrev];
                if (pos < Base.K_NUM_REP_DISTANCES)
                {
                    if (pos == 0)
                    {
                        _reps[0] = opt._backs0;
                        _reps[1] = opt._backs1;
                        _reps[2] = opt._backs2;
                        _reps[3] = opt._backs3;
                    }
                    else if (pos == 1)
                    {
                        _reps[0] = opt._backs1;
                        _reps[1] = opt._backs0;
                        _reps[2] = opt._backs2;
                        _reps[3] = opt._backs3;
                    }
                    else if (pos == 2)
                    {
                        _reps[0] = opt._backs2;
                        _reps[1] = opt._backs0;
                        _reps[2] = opt._backs1;
                        _reps[3] = opt._backs3;
                    }
                    else
                    {
                        _reps[0] = opt._backs3;
                        _reps[1] = opt._backs0;
                        _reps[2] = opt._backs1;
                        _reps[3] = opt._backs2;
                    }
                }
                else
                {
                    _reps[0] = (pos - Base.K_NUM_REP_DISTANCES);
                    _reps[1] = opt._backs0;
                    _reps[2] = opt._backs1;
                    _reps[3] = opt._backs2;
                }
            }
            _optimum[cur]._state = state;
            _optimum[cur]._backs0 = _reps[0];
            _optimum[cur]._backs1 = _reps[1];
            _optimum[cur]._backs2 = _reps[2];
            _optimum[cur]._backs3 = _reps[3];
            var curPrice = _optimum[cur]._price;

            currentByte = _matchFinder.GetIndexByte(0 - 1);
            matchByte = _matchFinder.GetIndexByte((int)(0 - _reps[0] - 1 - 1));

            posState = (position & _posStateMask);

            var curAnd1Price =
                curPrice
                + _isMatch[(state._index << Base.K_NUM_POS_STATES_BITS_MAX) + posState].GetPrice0()
                + _literalEncoder
                    .GetSubCoder(position, _matchFinder.GetIndexByte(0 - 2))
                    .GetPrice(!state.IsCharState(), matchByte, currentByte);

            var nextOptimum = _optimum[cur + 1];

            var nextIsChar = false;
            if (curAnd1Price < nextOptimum._price)
            {
                nextOptimum._price = curAnd1Price;
                nextOptimum._posPrev = cur;
                nextOptimum.MakeAsChar();
                nextIsChar = true;
            }

            matchPrice =
                curPrice
                + _isMatch[(state._index << Base.K_NUM_POS_STATES_BITS_MAX) + posState].GetPrice1();
            repMatchPrice = matchPrice + _isRep[state._index].GetPrice1();

            if (
                matchByte == currentByte
                && !(nextOptimum._posPrev < cur && nextOptimum._backPrev == 0)
            )
            {
                var shortRepPrice = repMatchPrice + GetRepLen1Price(state, posState);
                if (shortRepPrice <= nextOptimum._price)
                {
                    nextOptimum._price = shortRepPrice;
                    nextOptimum._posPrev = cur;
                    nextOptimum.MakeAsShortRep();
                    nextIsChar = true;
                }
            }

            var numAvailableBytesFull = _matchFinder.GetNumAvailableBytes() + 1;
            numAvailableBytesFull = Math.Min(K_NUM_OPTS - 1 - cur, numAvailableBytesFull);
            numAvailableBytes = numAvailableBytesFull;

            if (numAvailableBytes < 2)
            {
                continue;
            }
            if (numAvailableBytes > _numFastBytes)
            {
                numAvailableBytes = _numFastBytes;
            }
            if (!nextIsChar && matchByte != currentByte)
            {
                // try Literal + rep0
                var t = Math.Min(numAvailableBytesFull - 1, _numFastBytes);
                var lenTest2 = _matchFinder.GetMatchLen(0, _reps[0], t);
                if (lenTest2 >= 2)
                {
                    var state2 = state;
                    state2.UpdateChar();
                    var posStateNext = (position + 1) & _posStateMask;
                    var nextRepMatchPrice =
                        curAnd1Price
                        + _isMatch[(state2._index << Base.K_NUM_POS_STATES_BITS_MAX) + posStateNext]
                            .GetPrice1()
                        + _isRep[state2._index].GetPrice1();
                    {
                        var offset = cur + 1 + lenTest2;
                        while (lenEnd < offset)
                        {
                            _optimum[++lenEnd]._price = K_IFINITY_PRICE;
                        }
                        var curAndLenPrice =
                            nextRepMatchPrice + GetRepPrice(0, lenTest2, state2, posStateNext);
                        var optimum = _optimum[offset];
                        if (curAndLenPrice < optimum._price)
                        {
                            optimum._price = curAndLenPrice;
                            optimum._posPrev = cur + 1;
                            optimum._backPrev = 0;
                            optimum._prev1IsChar = true;
                            optimum._prev2 = false;
                        }
                    }
                }
            }

            uint startLen = 2; // speed optimization

            for (uint repIndex = 0; repIndex < Base.K_NUM_REP_DISTANCES; repIndex++)
            {
                var lenTest = _matchFinder.GetMatchLen(0 - 1, _reps[repIndex], numAvailableBytes);
                if (lenTest < 2)
                {
                    continue;
                }
                var lenTestTemp = lenTest;
                do
                {
                    while (lenEnd < cur + lenTest)
                    {
                        _optimum[++lenEnd]._price = K_IFINITY_PRICE;
                    }
                    var curAndLenPrice =
                        repMatchPrice + GetRepPrice(repIndex, lenTest, state, posState);
                    var optimum = _optimum[cur + lenTest];
                    if (curAndLenPrice < optimum._price)
                    {
                        optimum._price = curAndLenPrice;
                        optimum._posPrev = cur;
                        optimum._backPrev = repIndex;
                        optimum._prev1IsChar = false;
                    }
                } while (--lenTest >= 2);
                lenTest = lenTestTemp;

                if (repIndex == 0)
                {
                    startLen = lenTest + 1;
                }

                // if (_maxMode)
                if (lenTest < numAvailableBytesFull)
                {
                    var t = Math.Min(numAvailableBytesFull - 1 - lenTest, _numFastBytes);
                    var lenTest2 = _matchFinder.GetMatchLen((int)lenTest, _reps[repIndex], t);
                    if (lenTest2 >= 2)
                    {
                        var state2 = state;
                        state2.UpdateRep();
                        var posStateNext = (position + lenTest) & _posStateMask;
                        var curAndLenCharPrice =
                            repMatchPrice
                            + GetRepPrice(repIndex, lenTest, state, posState)
                            + _isMatch[
                                (state2._index << Base.K_NUM_POS_STATES_BITS_MAX) + posStateNext
                            ]
                                .GetPrice0()
                            + _literalEncoder
                                .GetSubCoder(
                                    position + lenTest,
                                    _matchFinder.GetIndexByte((int)lenTest - 1 - 1)
                                )
                                .GetPrice(
                                    true,
                                    _matchFinder.GetIndexByte(
                                        (int)lenTest - 1 - (int)(_reps[repIndex] + 1)
                                    ),
                                    _matchFinder.GetIndexByte((int)lenTest - 1)
                                );
                        state2.UpdateChar();
                        posStateNext = (position + lenTest + 1) & _posStateMask;
                        var nextMatchPrice =
                            curAndLenCharPrice
                            + _isMatch[
                                (state2._index << Base.K_NUM_POS_STATES_BITS_MAX) + posStateNext
                            ]
                                .GetPrice1();
                        var nextRepMatchPrice = nextMatchPrice + _isRep[state2._index].GetPrice1();

                        // for(; lenTest2 >= 2; lenTest2--)
                        {
                            var offset = lenTest + 1 + lenTest2;
                            while (lenEnd < cur + offset)
                            {
                                _optimum[++lenEnd]._price = K_IFINITY_PRICE;
                            }
                            var curAndLenPrice =
                                nextRepMatchPrice + GetRepPrice(0, lenTest2, state2, posStateNext);
                            var optimum = _optimum[cur + offset];
                            if (curAndLenPrice < optimum._price)
                            {
                                optimum._price = curAndLenPrice;
                                optimum._posPrev = cur + lenTest + 1;
                                optimum._backPrev = 0;
                                optimum._prev1IsChar = true;
                                optimum._prev2 = true;
                                optimum._posPrev2 = cur;
                                optimum._backPrev2 = repIndex;
                            }
                        }
                    }
                }
            }

            if (newLen > numAvailableBytes)
            {
                newLen = numAvailableBytes;
                for (
                    numDistancePairs = 0;
                    newLen > _matchDistances[numDistancePairs];
                    numDistancePairs += 2
                )
                {
                    ;
                }
                _matchDistances[numDistancePairs] = newLen;
                numDistancePairs += 2;
            }
            if (newLen >= startLen)
            {
                normalMatchPrice = matchPrice + _isRep[state._index].GetPrice0();
                while (lenEnd < cur + newLen)
                {
                    _optimum[++lenEnd]._price = K_IFINITY_PRICE;
                }

                uint offs = 0;
                while (startLen > _matchDistances[offs])
                {
                    offs += 2;
                }

                for (var lenTest = startLen; ; lenTest++)
                {
                    var curBack = _matchDistances[offs + 1];
                    var curAndLenPrice =
                        normalMatchPrice + GetPosLenPrice(curBack, lenTest, posState);
                    var optimum = _optimum[cur + lenTest];
                    if (curAndLenPrice < optimum._price)
                    {
                        optimum._price = curAndLenPrice;
                        optimum._posPrev = cur;
                        optimum._backPrev = curBack + Base.K_NUM_REP_DISTANCES;
                        optimum._prev1IsChar = false;
                    }

                    if (lenTest == _matchDistances[offs])
                    {
                        if (lenTest < numAvailableBytesFull)
                        {
                            var t = Math.Min(numAvailableBytesFull - 1 - lenTest, _numFastBytes);
                            var lenTest2 = _matchFinder.GetMatchLen((int)lenTest, curBack, t);
                            if (lenTest2 >= 2)
                            {
                                var state2 = state;
                                state2.UpdateMatch();
                                var posStateNext = (position + lenTest) & _posStateMask;
                                var curAndLenCharPrice =
                                    curAndLenPrice
                                    + _isMatch[
                                        (state2._index << Base.K_NUM_POS_STATES_BITS_MAX)
                                            + posStateNext
                                    ]
                                        .GetPrice0()
                                    + _literalEncoder
                                        .GetSubCoder(
                                            position + lenTest,
                                            _matchFinder.GetIndexByte((int)lenTest - 1 - 1)
                                        )
                                        .GetPrice(
                                            true,
                                            _matchFinder.GetIndexByte(
                                                (int)lenTest - (int)(curBack + 1) - 1
                                            ),
                                            _matchFinder.GetIndexByte((int)lenTest - 1)
                                        );
                                state2.UpdateChar();
                                posStateNext = (position + lenTest + 1) & _posStateMask;
                                var nextMatchPrice =
                                    curAndLenCharPrice
                                    + _isMatch[
                                        (state2._index << Base.K_NUM_POS_STATES_BITS_MAX)
                                            + posStateNext
                                    ]
                                        .GetPrice1();
                                var nextRepMatchPrice =
                                    nextMatchPrice + _isRep[state2._index].GetPrice1();

                                var offset = lenTest + 1 + lenTest2;
                                while (lenEnd < cur + offset)
                                {
                                    _optimum[++lenEnd]._price = K_IFINITY_PRICE;
                                }
                                curAndLenPrice =
                                    nextRepMatchPrice
                                    + GetRepPrice(0, lenTest2, state2, posStateNext);
                                optimum = _optimum[cur + offset];
                                if (curAndLenPrice < optimum._price)
                                {
                                    optimum._price = curAndLenPrice;
                                    optimum._posPrev = cur + lenTest + 1;
                                    optimum._backPrev = 0;
                                    optimum._prev1IsChar = true;
                                    optimum._prev2 = true;
                                    optimum._posPrev2 = cur;
                                    optimum._backPrev2 = curBack + Base.K_NUM_REP_DISTANCES;
                                }
                            }
                        }
                        offs += 2;
                        if (offs == numDistancePairs)
                        {
                            break;
                        }
                    }
                }
            }
        }
    }

    private bool ChangePair(uint smallDist, uint bigDist)
    {
        const int kDif = 7;
        return (smallDist < ((uint)(1) << (32 - kDif)) && bigDist >= (smallDist << kDif));
    }

    private void WriteEndMarker(uint posState)
    {
        if (!_writeEndMark)
        {
            return;
        }

        _isMatch[(_state._index << Base.K_NUM_POS_STATES_BITS_MAX) + posState]
            .Encode(_rangeEncoder, 1);
        _isRep[_state._index].Encode(_rangeEncoder, 0);
        _state.UpdateMatch();
        var len = Base.K_MATCH_MIN_LEN;
        _lenEncoder.Encode(_rangeEncoder, len - Base.K_MATCH_MIN_LEN, posState);
        uint posSlot = (1 << Base.K_NUM_POS_SLOT_BITS) - 1;
        var lenToPosState = Base.GetLenToPosState(len);
        _posSlotEncoder[lenToPosState].Encode(_rangeEncoder, posSlot);
        var footerBits = 30;
        var posReduced = (((uint)1) << footerBits) - 1;
        _rangeEncoder.EncodeDirectBits(
            posReduced >> Base.K_NUM_ALIGN_BITS,
            footerBits - Base.K_NUM_ALIGN_BITS
        );
        _posAlignEncoder.ReverseEncode(_rangeEncoder, posReduced & Base.K_ALIGN_MASK);
    }

    private void Flush(uint nowPos)
    {
        ReleaseMfStream();
        WriteEndMarker(nowPos & _posStateMask);
        _rangeEncoder.FlushData();
        _rangeEncoder.FlushStream();
    }

    public void CodeOneBlock(out long inSize, out long outSize, out bool finished)
    {
        inSize = 0;
        outSize = 0;
        finished = true;

        if (_inStream != null)
        {
            _matchFinder.SetStream(_inStream);
            _needReleaseMfStream = true;
            _inStream = null;
        }

        if (_finished)
        {
            return;
        }
        _finished = true;

        var progressPosValuePrev = _nowPos64;
        if (_nowPos64 == 0)
        {
            if (_trainSize > 0)
            {
                for (
                    ;
                    _trainSize > 0 && (!_processingMode || !_matchFinder.IsDataStarved);
                    _trainSize--
                )
                {
                    _matchFinder.Skip(1);
                }
                if (_trainSize == 0)
                {
                    _previousByte = _matchFinder.GetIndexByte(-1);
                }
            }
            if (_processingMode && _matchFinder.IsDataStarved)
            {
                _finished = false;
                return;
            }
            if (_matchFinder.GetNumAvailableBytes() == 0)
            {
                Flush((uint)_nowPos64);
                return;
            }
            // it's not used
            ReadMatchDistances(out var len, out var numDistancePairs);
            var posState = (uint)(_nowPos64) & _posStateMask;
            _isMatch[(_state._index << Base.K_NUM_POS_STATES_BITS_MAX) + posState]
                .Encode(_rangeEncoder, 0);
            _state.UpdateChar();
            var curByte = _matchFinder.GetIndexByte((int)(0 - _additionalOffset));
            _literalEncoder
                .GetSubCoder((uint)(_nowPos64), _previousByte)
                .Encode(_rangeEncoder, curByte);
            _previousByte = curByte;
            _additionalOffset--;
            _nowPos64++;
        }
        if (_processingMode && _matchFinder.IsDataStarved)
        {
            _finished = false;
            return;
        }
        if (_matchFinder.GetNumAvailableBytes() == 0)
        {
            Flush((uint)_nowPos64);
            return;
        }
        while (true)
        {
            if (_processingMode && _matchFinder.IsDataStarved)
            {
                _finished = false;
                return;
            }

            var len = GetOptimum((uint)_nowPos64, out var pos);

            var posState = ((uint)_nowPos64) & _posStateMask;
            var complexState = (_state._index << Base.K_NUM_POS_STATES_BITS_MAX) + posState;
            if (len == 1 && pos == 0xFFFFFFFF)
            {
                _isMatch[complexState].Encode(_rangeEncoder, 0);
                var curByte = _matchFinder.GetIndexByte((int)(0 - _additionalOffset));
                var subCoder = _literalEncoder.GetSubCoder((uint)_nowPos64, _previousByte);
                if (!_state.IsCharState())
                {
                    var matchByte = _matchFinder.GetIndexByte(
                        (int)(0 - _repDistances[0] - 1 - _additionalOffset)
                    );
                    subCoder.EncodeMatched(_rangeEncoder, matchByte, curByte);
                }
                else
                {
                    subCoder.Encode(_rangeEncoder, curByte);
                }
                _previousByte = curByte;
                _state.UpdateChar();
            }
            else
            {
                _isMatch[complexState].Encode(_rangeEncoder, 1);
                if (pos < Base.K_NUM_REP_DISTANCES)
                {
                    _isRep[_state._index].Encode(_rangeEncoder, 1);
                    if (pos == 0)
                    {
                        _isRepG0[_state._index].Encode(_rangeEncoder, 0);
                        if (len == 1)
                        {
                            _isRep0Long[complexState].Encode(_rangeEncoder, 0);
                        }
                        else
                        {
                            _isRep0Long[complexState].Encode(_rangeEncoder, 1);
                        }
                    }
                    else
                    {
                        _isRepG0[_state._index].Encode(_rangeEncoder, 1);
                        if (pos == 1)
                        {
                            _isRepG1[_state._index].Encode(_rangeEncoder, 0);
                        }
                        else
                        {
                            _isRepG1[_state._index].Encode(_rangeEncoder, 1);
                            _isRepG2[_state._index].Encode(_rangeEncoder, pos - 2);
                        }
                    }
                    if (len == 1)
                    {
                        _state.UpdateShortRep();
                    }
                    else
                    {
                        _repMatchLenEncoder.Encode(
                            _rangeEncoder,
                            len - Base.K_MATCH_MIN_LEN,
                            posState
                        );
                        _state.UpdateRep();
                    }
                    var distance = _repDistances[pos];
                    if (pos != 0)
                    {
                        for (var i = pos; i >= 1; i--)
                        {
                            _repDistances[i] = _repDistances[i - 1];
                        }
                        _repDistances[0] = distance;
                    }
                }
                else
                {
                    _isRep[_state._index].Encode(_rangeEncoder, 0);
                    _state.UpdateMatch();
                    _lenEncoder.Encode(_rangeEncoder, len - Base.K_MATCH_MIN_LEN, posState);
                    pos -= Base.K_NUM_REP_DISTANCES;
                    var posSlot = GetPosSlot(pos);
                    var lenToPosState = Base.GetLenToPosState(len);
                    _posSlotEncoder[lenToPosState].Encode(_rangeEncoder, posSlot);

                    if (posSlot >= Base.K_START_POS_MODEL_INDEX)
                    {
                        var footerBits = (int)((posSlot >> 1) - 1);
                        var baseVal = ((2 | (posSlot & 1)) << footerBits);
                        var posReduced = pos - baseVal;

                        if (posSlot < Base.K_END_POS_MODEL_INDEX)
                        {
                            BitTreeEncoder.ReverseEncode(
                                _posEncoders,
                                baseVal - posSlot - 1,
                                _rangeEncoder,
                                footerBits,
                                posReduced
                            );
                        }
                        else
                        {
                            _rangeEncoder.EncodeDirectBits(
                                posReduced >> Base.K_NUM_ALIGN_BITS,
                                footerBits - Base.K_NUM_ALIGN_BITS
                            );
                            _posAlignEncoder.ReverseEncode(
                                _rangeEncoder,
                                posReduced & Base.K_ALIGN_MASK
                            );
                            _alignPriceCount++;
                        }
                    }
                    var distance = pos;
                    for (var i = Base.K_NUM_REP_DISTANCES - 1; i >= 1; i--)
                    {
                        _repDistances[i] = _repDistances[i - 1];
                    }
                    _repDistances[0] = distance;
                    _matchPriceCount++;
                }
                _previousByte = _matchFinder.GetIndexByte((int)(len - 1 - _additionalOffset));
            }
            _additionalOffset -= len;
            _nowPos64 += len;
            if (_additionalOffset == 0)
            {
                // if (!_fastMode)
                if (_matchPriceCount >= (1 << 7))
                {
                    FillDistancesPrices();
                }
                if (_alignPriceCount >= Base.K_ALIGN_TABLE_SIZE)
                {
                    FillAlignPrices();
                }
                inSize = _nowPos64;
                outSize = _rangeEncoder.GetProcessedSizeAdd();
                if (_processingMode && _matchFinder.IsDataStarved)
                {
                    _finished = false;
                    return;
                }
                if (_matchFinder.GetNumAvailableBytes() == 0)
                {
                    Flush((uint)_nowPos64);
                    return;
                }

                if (_nowPos64 - progressPosValuePrev >= (1 << 12))
                {
                    _finished = false;
                    finished = false;
                    return;
                }
            }
        }
    }

    private void ReleaseMfStream()
    {
        if (_matchFinder != null && _needReleaseMfStream)
        {
            _matchFinder.ReleaseStream();
            _needReleaseMfStream = false;
        }
    }

    private void SetOutStream(Stream outStream) => _rangeEncoder.SetStream(outStream);

    private void ReleaseOutStream() => _rangeEncoder.ReleaseStream();

    private void ReleaseStreams()
    {
        ReleaseMfStream();
        ReleaseOutStream();
    }

    public void SetStreams(Stream inStream, Stream outStream, long inSize, long outSize)
    {
        _inStream = inStream;
        _finished = false;
        Create();
        SetOutStream(outStream);
        Init();
        _matchFinder.Init();

        // if (!_fastMode)
        {
            FillDistancesPrices();
            FillAlignPrices();
        }

        _lenEncoder.SetTableSize(_numFastBytes + 1 - Base.K_MATCH_MIN_LEN);
        _lenEncoder.UpdateTables((uint)1 << _posStateBits);
        _repMatchLenEncoder.SetTableSize(_numFastBytes + 1 - Base.K_MATCH_MIN_LEN);
        _repMatchLenEncoder.UpdateTables((uint)1 << _posStateBits);

        _nowPos64 = 0;
    }

    public void Code(
        Stream inStream,
        Stream outStream,
        long inSize,
        long outSize,
        ICodeProgress progress
    )
    {
        _needReleaseMfStream = false;
        _processingMode = false;
        try
        {
            SetStreams(inStream, outStream, inSize, outSize);
            while (true)
            {
                CodeOneBlock(out var processedInSize, out var processedOutSize, out var finished);
                if (finished)
                {
                    return;
                }
                progress?.SetProgress(processedInSize, processedOutSize);
            }
        }
        finally
        {
            ReleaseStreams();
        }
    }

    public long Code(Stream inStream, bool final)
    {
        _matchFinder.SetStream(inStream);
        _processingMode = !final;
        try
        {
            while (true)
            {
                CodeOneBlock(out var processedInSize, out var processedOutSize, out var finished);
                if (finished)
                {
                    return processedInSize;
                }
            }
        }
        finally
        {
            _matchFinder.ReleaseStream();
            if (final)
            {
                ReleaseStreams();
            }
        }
    }

    public void Train(Stream trainStream)
    {
        if (_nowPos64 > 0)
        {
            throw new InvalidOperationException();
        }
        _trainSize = (uint)trainStream.Length;
        if (_trainSize > 0)
        {
            _matchFinder.SetStream(trainStream);
            for (; _trainSize > 0 && !_matchFinder.IsDataStarved; _trainSize--)
            {
                _matchFinder.Skip(1);
            }
            if (_trainSize == 0)
            {
                _previousByte = _matchFinder.GetIndexByte(-1);
            }
            _matchFinder.ReleaseStream();
        }
    }

    private const int K_PROP_SIZE = 5;
    private readonly byte[] _properties = new byte[K_PROP_SIZE];

    public void WriteCoderProperties(Stream outStream)
    {
        WriteCoderProperties(_properties);
        outStream.Write(_properties, 0, K_PROP_SIZE);
    }

    public void WriteCoderProperties(Span<byte> span)
    {
        span[0] = (byte)(
            (((_posStateBits * 5) + _numLiteralPosStateBits) * 9) + _numLiteralContextBits
        );
        for (var i = 0; i < 4; i++)
        {
            span[1 + i] = (byte)((_dictionarySize >> (8 * i)) & 0xFF);
        }
    }

    private readonly uint[] _tempPrices = new uint[Base.K_NUM_FULL_DISTANCES];
    private uint _matchPriceCount;

    private void FillDistancesPrices()
    {
        for (var i = Base.K_START_POS_MODEL_INDEX; i < Base.K_NUM_FULL_DISTANCES; i++)
        {
            var posSlot = GetPosSlot(i);
            var footerBits = (int)((posSlot >> 1) - 1);
            var baseVal = ((2 | (posSlot & 1)) << footerBits);
            _tempPrices[i] = BitTreeEncoder.ReverseGetPrice(
                _posEncoders,
                baseVal - posSlot - 1,
                footerBits,
                i - baseVal
            );
        }

        for (uint lenToPosState = 0; lenToPosState < Base.K_NUM_LEN_TO_POS_STATES; lenToPosState++)
        {
            uint posSlot;
            var encoder = _posSlotEncoder[lenToPosState];

            var st = (lenToPosState << Base.K_NUM_POS_SLOT_BITS);
            for (posSlot = 0; posSlot < _distTableSize; posSlot++)
            {
                _posSlotPrices[st + posSlot] = encoder.GetPrice(posSlot);
            }
            for (posSlot = Base.K_END_POS_MODEL_INDEX; posSlot < _distTableSize; posSlot++)
            {
                _posSlotPrices[st + posSlot] += (
                    (((posSlot >> 1) - 1) - Base.K_NUM_ALIGN_BITS)
                    << BitEncoder.K_NUM_BIT_PRICE_SHIFT_BITS
                );
            }

            var st2 = lenToPosState * Base.K_NUM_FULL_DISTANCES;
            uint i;
            for (i = 0; i < Base.K_START_POS_MODEL_INDEX; i++)
            {
                _distancesPrices[st2 + i] = _posSlotPrices[st + i];
            }
            for (; i < Base.K_NUM_FULL_DISTANCES; i++)
            {
                _distancesPrices[st2 + i] = _posSlotPrices[st + GetPosSlot(i)] + _tempPrices[i];
            }
        }
        _matchPriceCount = 0;
    }

    private void FillAlignPrices()
    {
        for (uint i = 0; i < Base.K_ALIGN_TABLE_SIZE; i++)
        {
            _alignPrices[i] = _posAlignEncoder.ReverseGetPrice(i);
        }
        _alignPriceCount = 0;
    }

    private static readonly string[] K_MATCH_FINDER_I_DS = { "BT2", "BT4" };

    private static int FindMatchFinder(string s)
    {
        for (var m = 0; m < K_MATCH_FINDER_I_DS.Length; m++)
        {
            if (string.Equals(s, K_MATCH_FINDER_I_DS[m], StringComparison.OrdinalIgnoreCase))
            {
                return m;
            }
        }
        return -1;
    }

    public void SetCoderProperties(
        ReadOnlySpan<CoderPropId> propIDs,
        ReadOnlySpan<object> properties
    )
    {
        for (var i = 0; i < properties.Length; i++)
        {
            var prop = properties[i];
            switch (propIDs[i])
            {
                case CoderPropId.NumFastBytes:
                {
                    if (prop is not int)
                    {
                        throw new InvalidParamException();
                    }
                    var numFastBytes = (int)prop;
                    if (numFastBytes < 5 || numFastBytes > Base.K_MATCH_MAX_LEN)
                    {
                        throw new InvalidParamException();
                    }
                    _numFastBytes = (uint)numFastBytes;
                    break;
                }
                case CoderPropId.Algorithm:
                {
                    /*
                    if (!(prop is Int32))
                        throw new InvalidParamException();
                    Int32 maximize = (Int32)prop;
                    _fastMode = (maximize == 0);
                    _maxMode = (maximize >= 2);
                    */
                    break;
                }
                case CoderPropId.MatchFinder:
                {
                    if (prop is not string)
                    {
                        throw new InvalidParamException();
                    }
                    var matchFinderIndexPrev = _matchFinderType;
                    var m = FindMatchFinder(((string)prop));
                    if (m < 0)
                    {
                        throw new InvalidParamException();
                    }
                    _matchFinderType = (EMatchFinderType)m;
                    if (_matchFinder != null && matchFinderIndexPrev != _matchFinderType)
                    {
                        _dictionarySizePrev = 0xFFFFFFFF;
                        _matchFinder = null;
                    }
                    break;
                }
                case CoderPropId.DictionarySize:
                {
                    const int kDicLogSizeMaxCompress = 30;
                    if (prop is not int)
                    {
                        throw new InvalidParamException();
                    }
                    ;
                    var dictionarySize = (int)prop;
                    if (
                        dictionarySize < (uint)(1 << Base.K_DIC_LOG_SIZE_MIN)
                        || dictionarySize > (uint)(1 << kDicLogSizeMaxCompress)
                    )
                    {
                        throw new InvalidParamException();
                    }
                    _dictionarySize = (uint)dictionarySize;
                    int dicLogSize;
                    for (dicLogSize = 0; dicLogSize < (uint)kDicLogSizeMaxCompress; dicLogSize++)
                    {
                        if (dictionarySize <= ((uint)(1) << dicLogSize))
                        {
                            break;
                        }
                    }
                    _distTableSize = (uint)dicLogSize * 2;
                    break;
                }
                case CoderPropId.PosStateBits:
                {
                    if (prop is not int)
                    {
                        throw new InvalidParamException();
                    }
                    var v = (int)prop;
                    if (v < 0 || v > (uint)Base.K_NUM_POS_STATES_BITS_ENCODING_MAX)
                    {
                        throw new InvalidParamException();
                    }
                    _posStateBits = v;
                    _posStateMask = (((uint)1) << _posStateBits) - 1;
                    break;
                }
                case CoderPropId.LitPosBits:
                {
                    if (prop is not int)
                    {
                        throw new InvalidParamException();
                    }
                    var v = (int)prop;
                    if (v < 0 || v > Base.K_NUM_LIT_POS_STATES_BITS_ENCODING_MAX)
                    {
                        throw new InvalidParamException();
                    }
                    _numLiteralPosStateBits = v;
                    break;
                }
                case CoderPropId.LitContextBits:
                {
                    if (prop is not int)
                    {
                        throw new InvalidParamException();
                    }
                    var v = (int)prop;
                    if (v < 0 || v > Base.K_NUM_LIT_CONTEXT_BITS_MAX)
                    {
                        throw new InvalidParamException();
                    }
                    ;
                    _numLiteralContextBits = v;
                    break;
                }
                case CoderPropId.EndMarker:
                {
                    if (prop is not bool)
                    {
                        throw new InvalidParamException();
                    }
                    SetWriteEndMarkerMode((bool)prop);
                    break;
                }
                default:
                    throw new InvalidParamException();
            }
        }
    }

    private uint _trainSize;

    public void SetTrainSize(uint trainSize) => _trainSize = trainSize;
}
