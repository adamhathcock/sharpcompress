namespace SharpCompress.Compressor.LZMA
{
    using SharpCompress.Compressor.LZMA.LZ;
    using SharpCompress.Compressor.LZMA.RangeCoder;
    using System;
    using System.IO;
    using System.Runtime.InteropServices;

    internal class Encoder : ICoder, ISetCoderProperties, IWriteCoderProperties
    {
        private uint _additionalOffset;
        private uint _alignPriceCount;
        private uint[] _alignPrices;
        private uint _dictionarySize;
        private uint _dictionarySizePrev;
        private uint[] _distancesPrices;
        private uint _distTableSize;
        private bool _finished;
        private Stream _inStream;
        private BitEncoder[] _isMatch;
        private BitEncoder[] _isRep;
        private BitEncoder[] _isRep0Long;
        private BitEncoder[] _isRepG0;
        private BitEncoder[] _isRepG1;
        private BitEncoder[] _isRepG2;
        private LenPriceTableEncoder _lenEncoder;
        private LiteralEncoder _literalEncoder;
        private uint _longestMatchLength;
        private bool _longestMatchWasFound;
        private uint[] _matchDistances;
        private BinTree _matchFinder;
        private EMatchFinderType _matchFinderType;
        private uint _matchPriceCount;
        private bool _needReleaseMFStream;
        private uint _numDistancePairs;
        private uint _numFastBytes;
        private uint _numFastBytesPrev;
        private int _numLiteralContextBits;
        private int _numLiteralPosStateBits;
        private Optimal[] _optimum;
        private uint _optimumCurrentIndex;
        private uint _optimumEndIndex;
        private BitTreeEncoder _posAlignEncoder;
        private BitEncoder[] _posEncoders;
        private BitTreeEncoder[] _posSlotEncoder;
        private uint[] _posSlotPrices;
        private int _posStateBits;
        private uint _posStateMask;
        private byte _previousByte;
        private bool _processingMode;
        private SharpCompress.Compressor.LZMA.RangeCoder.Encoder _rangeEncoder;
        private uint[] _repDistances;
        private LenPriceTableEncoder _repMatchLenEncoder;
        private Base.State _state;
        private uint _trainSize;
        private bool _writeEndMark;
        private static byte[] g_FastPos = new byte[0x800];
        private const int kDefaultDictionaryLogSize = 0x16;
        private const uint kIfinityPrice = 0xfffffff;
        private static string[] kMatchFinderIDs = new string[] { "BT2", "BT4" };
        private const uint kNumFastBytesDefault = 0x20;
        private const uint kNumLenSpecSymbols = 0x10;
        private const uint kNumOpts = 0x1000;
        private const int kPropSize = 5;
        private long nowPos64;
        private byte[] properties;
        private uint[] repLens;
        private uint[] reps;
        private uint[] tempPrices;

        static Encoder()
        {
            int index = 2;
            g_FastPos[0] = 0;
            g_FastPos[1] = 1;
            for (byte i = 2; i < 0x16; i = (byte) (i + 1))
            {
                uint num3 = ((uint) 1) << ((i >> 1) - 1);
                uint num4 = 0;
                while (num4 < num3)
                {
                    g_FastPos[index] = i;
                    num4++;
                    index++;
                }
            }
        }

        public Encoder()
        {
            int num;
            this._state = new Base.State();
            this._repDistances = new uint[4];
            this._optimum = new Optimal[0x1000];
            this._matchFinder = null;
            this._rangeEncoder = new SharpCompress.Compressor.LZMA.RangeCoder.Encoder();
            this._isMatch = new BitEncoder[0xc0];
            this._isRep = new BitEncoder[12];
            this._isRepG0 = new BitEncoder[12];
            this._isRepG1 = new BitEncoder[12];
            this._isRepG2 = new BitEncoder[12];
            this._isRep0Long = new BitEncoder[0xc0];
            this._posSlotEncoder = new BitTreeEncoder[4];
            this._posEncoders = new BitEncoder[0x72];
            this._posAlignEncoder = new BitTreeEncoder(4);
            this._lenEncoder = new LenPriceTableEncoder();
            this._repMatchLenEncoder = new LenPriceTableEncoder();
            this._literalEncoder = new LiteralEncoder();
            this._matchDistances = new uint[0x224];
            this._numFastBytes = 0x20;
            this._posSlotPrices = new uint[0x100];
            this._distancesPrices = new uint[0x200];
            this._alignPrices = new uint[0x10];
            this._distTableSize = 0x2c;
            this._posStateBits = 2;
            this._posStateMask = 3;
            this._numLiteralPosStateBits = 0;
            this._numLiteralContextBits = 3;
            this._dictionarySize = 0x400000;
            this._dictionarySizePrev = uint.MaxValue;
            this._numFastBytesPrev = uint.MaxValue;
            this._matchFinderType = EMatchFinderType.BT4;
            this._writeEndMark = false;
            this.reps = new uint[4];
            this.repLens = new uint[4];
            this.properties = new byte[5];
            this.tempPrices = new uint[0x80];
            this._trainSize = 0;
            for (num = 0; num < 0x1000L; num++)
            {
                this._optimum[num] = new Optimal();
            }
            for (num = 0; num < 4L; num++)
            {
                this._posSlotEncoder[num] = new BitTreeEncoder(6);
            }
        }

        private uint Backward(out uint backRes, uint cur)
        {
            this._optimumEndIndex = cur;
            uint posPrev = this._optimum[cur].PosPrev;
            uint backPrev = this._optimum[cur].BackPrev;
            do
            {
                if (this._optimum[cur].Prev1IsChar)
                {
                    this._optimum[posPrev].MakeAsChar();
                    this._optimum[posPrev].PosPrev = posPrev - 1;
                    if (this._optimum[cur].Prev2)
                    {
                        this._optimum[(int) ((IntPtr) (posPrev - 1))].Prev1IsChar = false;
                        this._optimum[(int) ((IntPtr) (posPrev - 1))].PosPrev = this._optimum[cur].PosPrev2;
                        this._optimum[(int) ((IntPtr) (posPrev - 1))].BackPrev = this._optimum[cur].BackPrev2;
                    }
                }
                uint index = posPrev;
                uint num4 = backPrev;
                backPrev = this._optimum[index].BackPrev;
                posPrev = this._optimum[index].PosPrev;
                this._optimum[index].BackPrev = num4;
                this._optimum[index].PosPrev = cur;
                cur = index;
            }
            while (cur > 0);
            backRes = this._optimum[0].BackPrev;
            this._optimumCurrentIndex = this._optimum[0].PosPrev;
            return this._optimumCurrentIndex;
        }

        private void BaseInit()
        {
            this._state.Init();
            this._previousByte = 0;
            for (uint i = 0; i < 4; i++)
            {
                this._repDistances[i] = 0;
            }
        }

        private bool ChangePair(uint smallDist, uint bigDist)
        {
            return ((smallDist < 0x2000000) && (bigDist >= (smallDist << 7)));
        }

        public long Code(Stream inStream, bool final)
        {
            long num3;
            this._matchFinder.SetStream(inStream);
            this._processingMode = !final;
            try
            {
                long num;
                long num2;
                bool flag;
                bool flag2;
                goto Label_0038;
            Label_001B:
                this.CodeOneBlock(out num, out num2, out flag);
                if (flag)
                {
                    return num;
                }
            Label_0038:
                flag2 = true;
                goto Label_001B;
            }
            finally
            {
                this._matchFinder.ReleaseStream();
                if (final)
                {
                    this.ReleaseStreams();
                }
            }
            return num3;
        }

        public void Code(Stream inStream, Stream outStream, long inSize, long outSize, ICodeProgress progress)
        {
            this._needReleaseMFStream = false;
            this._processingMode = false;
            try
            {
                long num;
                long num2;
                bool flag;
                bool flag2;
                this.SetStreams(inStream, outStream, inSize, outSize);
                goto Label_004C;
            Label_001E:
                this.CodeOneBlock(out num, out num2, out flag);
                if (flag)
                {
                    return;
                }
                if (progress != null)
                {
                    progress.SetProgress(num, num2);
                }
            Label_004C:
                flag2 = true;
                goto Label_001E;
            }
            finally
            {
                this.ReleaseStreams();
            }
        }

        public void CodeOneBlock(out long inSize, out long outSize, out bool finished)
        {
            long num;
            uint optimum;
            uint num4;
            byte indexByte;
            bool flag;
            inSize = 0L;
            outSize = 0L;
            finished = true;
            if (this._inStream != null)
            {
                this._matchFinder.SetStream(this._inStream);
                this._needReleaseMFStream = true;
                this._inStream = null;
            }
            if (!this._finished)
            {
                this._finished = true;
                num = this.nowPos64;
                if (this.nowPos64 == 0L)
                {
                    uint num3;
                    if (this._trainSize > 0)
                    {
                        while ((this._trainSize > 0) && (!this._processingMode || !this._matchFinder.IsDataStarved))
                        {
                            this._matchFinder.Skip(1);
                            this._trainSize--;
                        }
                        if (this._trainSize == 0)
                        {
                            this._previousByte = this._matchFinder.GetIndexByte(-1);
                        }
                    }
                    if (this._processingMode && this._matchFinder.IsDataStarved)
                    {
                        this._finished = false;
                        return;
                    }
                    if (this._matchFinder.GetNumAvailableBytes() == 0)
                    {
                        this.Flush((uint) this.nowPos64);
                        return;
                    }
                    this.ReadMatchDistances(out optimum, out num3);
                    num4 = ((uint) this.nowPos64) & this._posStateMask;
                    this._isMatch[(this._state.Index << 4) + num4].Encode(this._rangeEncoder, 0);
                    this._state.UpdateChar();
                    indexByte = this._matchFinder.GetIndexByte(0 - ((int) this._additionalOffset));
                    this._literalEncoder.GetSubCoder((uint) this.nowPos64, this._previousByte).Encode(this._rangeEncoder, indexByte);
                    this._previousByte = indexByte;
                    this._additionalOffset--;
                    this.nowPos64 += 1L;
                }
                if (this._processingMode && this._matchFinder.IsDataStarved)
                {
                    this._finished = false;
                }
                else
                {
                    if (this._matchFinder.GetNumAvailableBytes() != 0)
                    {
                        goto Label_080A;
                    }
                    this.Flush((uint) this.nowPos64);
                }
            }
            return;
        Label_080A:
            flag = true;
            if (this._processingMode && this._matchFinder.IsDataStarved)
            {
                this._finished = false;
            }
            else
            {
                uint num6;
                optimum = this.GetOptimum((uint) this.nowPos64, out num6);
                num4 = ((uint) this.nowPos64) & this._posStateMask;
                uint index = (this._state.Index << 4) + num4;
                if ((optimum == 1) && (num6 == uint.MaxValue))
                {
                    this._isMatch[index].Encode(this._rangeEncoder, 0);
                    indexByte = this._matchFinder.GetIndexByte(0 - ((int) this._additionalOffset));
                    LiteralEncoder.Encoder2 subCoder = this._literalEncoder.GetSubCoder((uint) this.nowPos64, this._previousByte);
                    if (!this._state.IsCharState())
                    {
                        byte matchByte = this._matchFinder.GetIndexByte((int) (((0 - this._repDistances[0]) - 1) - this._additionalOffset));
                        subCoder.EncodeMatched(this._rangeEncoder, matchByte, indexByte);
                    }
                    else
                    {
                        subCoder.Encode(this._rangeEncoder, indexByte);
                    }
                    this._previousByte = indexByte;
                    this._state.UpdateChar();
                }
                else
                {
                    uint num9;
                    uint num10;
                    this._isMatch[index].Encode(this._rangeEncoder, 1);
                    if (num6 < 4)
                    {
                        this._isRep[this._state.Index].Encode(this._rangeEncoder, 1);
                        if (num6 == 0)
                        {
                            this._isRepG0[this._state.Index].Encode(this._rangeEncoder, 0);
                            if (optimum == 1)
                            {
                                this._isRep0Long[index].Encode(this._rangeEncoder, 0);
                            }
                            else
                            {
                                this._isRep0Long[index].Encode(this._rangeEncoder, 1);
                            }
                        }
                        else
                        {
                            this._isRepG0[this._state.Index].Encode(this._rangeEncoder, 1);
                            if (num6 == 1)
                            {
                                this._isRepG1[this._state.Index].Encode(this._rangeEncoder, 0);
                            }
                            else
                            {
                                this._isRepG1[this._state.Index].Encode(this._rangeEncoder, 1);
                                this._isRepG2[this._state.Index].Encode(this._rangeEncoder, num6 - 2);
                            }
                        }
                        if (optimum == 1)
                        {
                            this._state.UpdateShortRep();
                        }
                        else
                        {
                            this._repMatchLenEncoder.Encode(this._rangeEncoder, optimum - 2, num4);
                            this._state.UpdateRep();
                        }
                        num9 = this._repDistances[num6];
                        if (num6 != 0)
                        {
                            for (num10 = num6; num10 >= 1; num10--)
                            {
                                this._repDistances[num10] = this._repDistances[(int) ((IntPtr) (num10 - 1))];
                            }
                            this._repDistances[0] = num9;
                        }
                    }
                    else
                    {
                        this._isRep[this._state.Index].Encode(this._rangeEncoder, 0);
                        this._state.UpdateMatch();
                        this._lenEncoder.Encode(this._rangeEncoder, optimum - 2, num4);
                        num6 -= 4;
                        uint posSlot = GetPosSlot(num6);
                        uint lenToPosState = Base.GetLenToPosState(optimum);
                        this._posSlotEncoder[lenToPosState].Encode(this._rangeEncoder, posSlot);
                        if (posSlot >= 4)
                        {
                            int numBitLevels = ((int) (posSlot >> 1)) - 1;
                            uint num14 = (uint) ((2 | (posSlot & 1)) << (numBitLevels & 0x1f));
                            uint symbol = num6 - num14;
                            if (posSlot < 14)
                            {
                                BitTreeEncoder.ReverseEncode(this._posEncoders, (num14 - posSlot) - 1, this._rangeEncoder, numBitLevels, symbol);
                            }
                            else
                            {
                                this._rangeEncoder.EncodeDirectBits(symbol >> 4, numBitLevels - 4);
                                this._posAlignEncoder.ReverseEncode(this._rangeEncoder, symbol & 15);
                                this._alignPriceCount++;
                            }
                        }
                        num9 = num6;
                        for (num10 = 3; num10 >= 1; num10--)
                        {
                            this._repDistances[num10] = this._repDistances[(int) ((IntPtr) (num10 - 1))];
                        }
                        this._repDistances[0] = num9;
                        this._matchPriceCount++;
                    }
                    this._previousByte = this._matchFinder.GetIndexByte((int) ((optimum - 1) - this._additionalOffset));
                }
                this._additionalOffset -= optimum;
                this.nowPos64 += optimum;
                if (this._additionalOffset == 0)
                {
                    if (this._matchPriceCount >= 0x80)
                    {
                        this.FillDistancesPrices();
                    }
                    if (this._alignPriceCount >= 0x10)
                    {
                        this.FillAlignPrices();
                    }
                    inSize = this.nowPos64;
                    outSize = this._rangeEncoder.GetProcessedSizeAdd();
                    if (this._processingMode && this._matchFinder.IsDataStarved)
                    {
                        this._finished = false;
                        return;
                    }
                    if (this._matchFinder.GetNumAvailableBytes() == 0)
                    {
                        this.Flush((uint) this.nowPos64);
                        return;
                    }
                    if ((this.nowPos64 - num) >= 0x1000L)
                    {
                        this._finished = false;
                        finished = false;
                        return;
                    }
                }
                goto Label_080A;
            }
        }

        private void Create()
        {
            if (this._matchFinder == null)
            {
                BinTree tree = new BinTree();
                int numHashBytes = 4;
                if (this._matchFinderType == EMatchFinderType.BT2)
                {
                    numHashBytes = 2;
                }
                tree.SetType(numHashBytes);
                this._matchFinder = tree;
            }
            this._literalEncoder.Create(this._numLiteralPosStateBits, this._numLiteralContextBits);
            if ((this._dictionarySize != this._dictionarySizePrev) || (this._numFastBytesPrev != this._numFastBytes))
            {
                this._matchFinder.Create(this._dictionarySize, 0x1000, this._numFastBytes, 0x1112);
                this._dictionarySizePrev = this._dictionarySize;
                this._numFastBytesPrev = this._numFastBytes;
            }
        }

        private void FillAlignPrices()
        {
            for (uint i = 0; i < 0x10; i++)
            {
                this._alignPrices[i] = this._posAlignEncoder.ReverseGetPrice(i);
            }
            this._alignPriceCount = 0;
        }

        private void FillDistancesPrices()
        {
            uint posSlot;
            uint pos = 4;
            while (pos < 0x80)
            {
                posSlot = GetPosSlot(pos);
                int numBitLevels = ((int) (posSlot >> 1)) - 1;
                uint num4 = (uint) ((2 | (posSlot & 1)) << (numBitLevels & 0x1f));
                this.tempPrices[pos] = BitTreeEncoder.ReverseGetPrice(this._posEncoders, (num4 - posSlot) - 1, numBitLevels, pos - num4);
                pos++;
            }
            for (uint i = 0; i < 4; i++)
            {
                BitTreeEncoder encoder = this._posSlotEncoder[i];
                uint num6 = i << 6;
                posSlot = 0;
                while (posSlot < this._distTableSize)
                {
                    this._posSlotPrices[num6 + posSlot] = encoder.GetPrice(posSlot);
                    posSlot++;
                }
                for (posSlot = 14; posSlot < this._distTableSize; posSlot++)
                {
                    this._posSlotPrices[num6 + posSlot] += (uint) ((((posSlot >> 1) - 1) - 4) << 6);
                }
                uint num7 = i * 0x80;
                pos = 0;
                while (pos < 4)
                {
                    this._distancesPrices[num7 + pos] = this._posSlotPrices[num6 + pos];
                    pos++;
                }
                while (pos < 0x80)
                {
                    this._distancesPrices[num7 + pos] = this._posSlotPrices[num6 + GetPosSlot(pos)] + this.tempPrices[pos];
                    pos++;
                }
            }
            this._matchPriceCount = 0;
        }

        private static int FindMatchFinder(string s)
        {
            for (int i = 0; i < kMatchFinderIDs.Length; i++)
            {
                if (s == kMatchFinderIDs[i])
                {
                    return i;
                }
            }
            return -1;
        }

        private void Flush(uint nowPos)
        {
            this.ReleaseMFStream();
            this.WriteEndMarker(nowPos & this._posStateMask);
            this._rangeEncoder.FlushData();
            this._rangeEncoder.FlushStream();
        }

        private uint GetOptimum(uint position, out uint backRes)
        {
            uint num;
            uint num2;
            uint num3;
            uint num6;
            uint num12;
            uint num17;
            Optimal optimal;
            uint num19;
            uint num22;
            Base.State state;
            uint num28;
            uint num29;
            Base.State state2;
            uint num30;
            uint num31;
            uint num32;
            uint num35;
            uint num37;
            uint num38;
            bool flag2;
            if (this._optimumEndIndex != this._optimumCurrentIndex)
            {
                num = this._optimum[this._optimumCurrentIndex].PosPrev - this._optimumCurrentIndex;
                backRes = this._optimum[this._optimumCurrentIndex].BackPrev;
                this._optimumCurrentIndex = this._optimum[this._optimumCurrentIndex].PosPrev;
                return num;
            }
            this._optimumCurrentIndex = this._optimumEndIndex = 0;
            if (!this._longestMatchWasFound)
            {
                this.ReadMatchDistances(out num2, out num3);
            }
            else
            {
                num2 = this._longestMatchLength;
                num3 = this._numDistancePairs;
                this._longestMatchWasFound = false;
            }
            uint limit = this._matchFinder.GetNumAvailableBytes() + 1;
            if (limit < 2)
            {
                backRes = uint.MaxValue;
                return 1;
            }
            if (limit > 0x111)
            {
                limit = 0x111;
            }
            uint index = 0;
            for (num6 = 0; num6 < 4; num6++)
            {
                this.reps[num6] = this._repDistances[num6];
                this.repLens[num6] = this._matchFinder.GetMatchLen(-1, this.reps[num6], 0x111);
                if (this.repLens[num6] > this.repLens[index])
                {
                    index = num6;
                }
            }
            if (this.repLens[index] >= this._numFastBytes)
            {
                backRes = index;
                num = this.repLens[index];
                this.MovePos(num - 1);
                return num;
            }
            if (num2 >= this._numFastBytes)
            {
                backRes = this._matchDistances[(int) ((IntPtr) (num3 - 1))] + 4;
                this.MovePos(num2 - 1);
                return num2;
            }
            byte indexByte = this._matchFinder.GetIndexByte(-1);
            byte matchByte = this._matchFinder.GetIndexByte(((0 - ((int) this._repDistances[0])) - 1) - 1);
            if (((num2 < 2) && (indexByte != matchByte)) && (this.repLens[index] < 2))
            {
                backRes = uint.MaxValue;
                return 1;
            }
            this._optimum[0].State = this._state;
            uint posState = position & this._posStateMask;
            this._optimum[1].Price = this._isMatch[(this._state.Index << 4) + posState].GetPrice0() + this._literalEncoder.GetSubCoder(position, this._previousByte).GetPrice(!this._state.IsCharState(), matchByte, indexByte);
            this._optimum[1].MakeAsChar();
            uint num10 = this._isMatch[(this._state.Index << 4) + posState].GetPrice1();
            uint num11 = num10 + this._isRep[this._state.Index].GetPrice1();
            if (matchByte == indexByte)
            {
                num12 = num11 + this.GetRepLen1Price(this._state, posState);
                if (num12 < this._optimum[1].Price)
                {
                    this._optimum[1].Price = num12;
                    this._optimum[1].MakeAsShortRep();
                }
            }
            uint num13 = (num2 >= this.repLens[index]) ? num2 : this.repLens[index];
            if (num13 < 2)
            {
                backRes = this._optimum[1].BackPrev;
                return 1;
            }
            this._optimum[1].PosPrev = 0;
            this._optimum[0].Backs0 = this.reps[0];
            this._optimum[0].Backs1 = this.reps[1];
            this._optimum[0].Backs2 = this.reps[2];
            this._optimum[0].Backs3 = this.reps[3];
            uint len = num13;
            do
            {
                this._optimum[len--].Price = 0xfffffff;
            }
            while (len >= 2);
            for (num6 = 0; num6 < 4; num6++)
            {
                uint num15 = this.repLens[num6];
                if (num15 >= 2)
                {
                    uint num16 = num11 + this.GetPureRepPrice(num6, this._state, posState);
                    do
                    {
                        num17 = num16 + this._repMatchLenEncoder.GetPrice(num15 - 2, posState);
                        optimal = this._optimum[num15];
                        if (num17 < optimal.Price)
                        {
                            optimal.Price = num17;
                            optimal.PosPrev = 0;
                            optimal.BackPrev = num6;
                            optimal.Prev1IsChar = false;
                        }
                    }
                    while (--num15 >= 2);
                }
            }
            uint num18 = num10 + this._isRep[this._state.Index].GetPrice0();
            len = (this.repLens[0] >= 2) ? (this.repLens[0] + 1) : 2;
            if (len <= num2)
            {
                num19 = 0;
                while (len > this._matchDistances[num19])
                {
                    num19 += 2;
                }
                while (true)
                {
                    flag2 = true;
                    uint pos = this._matchDistances[(int) ((IntPtr) (num19 + 1))];
                    num17 = num18 + this.GetPosLenPrice(pos, len, posState);
                    optimal = this._optimum[len];
                    if (num17 < optimal.Price)
                    {
                        optimal.Price = num17;
                        optimal.PosPrev = 0;
                        optimal.BackPrev = pos + 4;
                        optimal.Prev1IsChar = false;
                    }
                    if (len == this._matchDistances[num19])
                    {
                        num19 += 2;
                        if (num19 == num3)
                        {
                            break;
                        }
                    }
                    len++;
                }
            }
            uint cur = 0;
        Label_133B:
            flag2 = true;
            cur++;
            if (cur == num13)
            {
                return this.Backward(out backRes, cur);
            }
            this.ReadMatchDistances(out num22, out num3);
            if (num22 >= this._numFastBytes)
            {
                this._numDistancePairs = num3;
                this._longestMatchLength = num22;
                this._longestMatchWasFound = true;
                return this.Backward(out backRes, cur);
            }
            position++;
            uint posPrev = this._optimum[cur].PosPrev;
            if (this._optimum[cur].Prev1IsChar)
            {
                posPrev--;
                if (this._optimum[cur].Prev2)
                {
                    state = this._optimum[this._optimum[cur].PosPrev2].State;
                    if (this._optimum[cur].BackPrev2 < 4)
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
                    state = this._optimum[posPrev].State;
                }
                state.UpdateChar();
            }
            else
            {
                state = this._optimum[posPrev].State;
            }
            if (posPrev == (cur - 1))
            {
                if (this._optimum[cur].IsShortRep())
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
                uint backPrev;
                if (this._optimum[cur].Prev1IsChar && this._optimum[cur].Prev2)
                {
                    posPrev = this._optimum[cur].PosPrev2;
                    backPrev = this._optimum[cur].BackPrev2;
                    state.UpdateRep();
                }
                else
                {
                    backPrev = this._optimum[cur].BackPrev;
                    if (backPrev < 4)
                    {
                        state.UpdateRep();
                    }
                    else
                    {
                        state.UpdateMatch();
                    }
                }
                Optimal optimal2 = this._optimum[posPrev];
                if (backPrev < 4)
                {
                    if (backPrev == 0)
                    {
                        this.reps[0] = optimal2.Backs0;
                        this.reps[1] = optimal2.Backs1;
                        this.reps[2] = optimal2.Backs2;
                        this.reps[3] = optimal2.Backs3;
                    }
                    else if (backPrev == 1)
                    {
                        this.reps[0] = optimal2.Backs1;
                        this.reps[1] = optimal2.Backs0;
                        this.reps[2] = optimal2.Backs2;
                        this.reps[3] = optimal2.Backs3;
                    }
                    else if (backPrev == 2)
                    {
                        this.reps[0] = optimal2.Backs2;
                        this.reps[1] = optimal2.Backs0;
                        this.reps[2] = optimal2.Backs1;
                        this.reps[3] = optimal2.Backs3;
                    }
                    else
                    {
                        this.reps[0] = optimal2.Backs3;
                        this.reps[1] = optimal2.Backs0;
                        this.reps[2] = optimal2.Backs1;
                        this.reps[3] = optimal2.Backs2;
                    }
                }
                else
                {
                    this.reps[0] = backPrev - 4;
                    this.reps[1] = optimal2.Backs0;
                    this.reps[2] = optimal2.Backs1;
                    this.reps[3] = optimal2.Backs2;
                }
            }
            this._optimum[cur].State = state;
            this._optimum[cur].Backs0 = this.reps[0];
            this._optimum[cur].Backs1 = this.reps[1];
            this._optimum[cur].Backs2 = this.reps[2];
            this._optimum[cur].Backs3 = this.reps[3];
            uint price = this._optimum[cur].Price;
            indexByte = this._matchFinder.GetIndexByte(-1);
            matchByte = this._matchFinder.GetIndexByte(((0 - ((int) this.reps[0])) - 1) - 1);
            posState = position & this._posStateMask;
            uint num26 = (price + this._isMatch[(state.Index << 4) + posState].GetPrice0()) + this._literalEncoder.GetSubCoder(position, this._matchFinder.GetIndexByte(-2)).GetPrice(!state.IsCharState(), matchByte, indexByte);
            Optimal optimal3 = this._optimum[(int) ((IntPtr) (cur + 1))];
            bool flag = false;
            if (num26 < optimal3.Price)
            {
                optimal3.Price = num26;
                optimal3.PosPrev = cur;
                optimal3.MakeAsChar();
                flag = true;
            }
            num10 = price + this._isMatch[(state.Index << 4) + posState].GetPrice1();
            num11 = num10 + this._isRep[state.Index].GetPrice1();
            if ((matchByte == indexByte) && ((optimal3.PosPrev >= cur) || (optimal3.BackPrev != 0)))
            {
                num12 = num11 + this.GetRepLen1Price(state, posState);
                if (num12 <= optimal3.Price)
                {
                    optimal3.Price = num12;
                    optimal3.PosPrev = cur;
                    optimal3.MakeAsShortRep();
                    flag = true;
                }
            }
            uint num27 = this._matchFinder.GetNumAvailableBytes() + 1;
            num27 = Math.Min(0xfff - cur, num27);
            limit = num27;
            if (limit < 2)
            {
                goto Label_133B;
            }
            if (limit > this._numFastBytes)
            {
                limit = this._numFastBytes;
            }
            if (!flag && (matchByte != indexByte))
            {
                num28 = Math.Min(num27 - 1, this._numFastBytes);
                num29 = this._matchFinder.GetMatchLen(0, this.reps[0], num28);
                if (num29 >= 2)
                {
                    state2 = state;
                    state2.UpdateChar();
                    num30 = (position + 1) & this._posStateMask;
                    num31 = (num26 + this._isMatch[(state2.Index << 4) + num30].GetPrice1()) + this._isRep[state2.Index].GetPrice1();
                    num32 = (cur + 1) + num29;
                    while (num13 < num32)
                    {
                        this._optimum[(int) ((IntPtr) (++num13))].Price = 0xfffffff;
                    }
                    num17 = num31 + this.GetRepPrice(0, num29, state2, num30);
                    optimal = this._optimum[num32];
                    if (num17 < optimal.Price)
                    {
                        optimal.Price = num17;
                        optimal.PosPrev = cur + 1;
                        optimal.BackPrev = 0;
                        optimal.Prev1IsChar = true;
                        optimal.Prev2 = false;
                    }
                }
            }
            uint num33 = 2;
            for (uint i = 0; i < 4; i++)
            {
                num35 = this._matchFinder.GetMatchLen(-1, this.reps[i], limit);
                if (num35 >= 2)
                {
                    uint num36 = num35;
                    do
                    {
                        while (num13 < (cur + num35))
                        {
                            this._optimum[(int) ((IntPtr) (++num13))].Price = 0xfffffff;
                        }
                        num17 = num11 + this.GetRepPrice(i, num35, state, posState);
                        optimal = this._optimum[cur + num35];
                        if (num17 < optimal.Price)
                        {
                            optimal.Price = num17;
                            optimal.PosPrev = cur;
                            optimal.BackPrev = i;
                            optimal.Prev1IsChar = false;
                        }
                    }
                    while (--num35 >= 2);
                    num35 = num36;
                    if (i == 0)
                    {
                        num33 = num35 + 1;
                    }
                    if (num35 < num27)
                    {
                        num28 = Math.Min((num27 - 1) - num35, this._numFastBytes);
                        num29 = this._matchFinder.GetMatchLen((int) num35, this.reps[i], num28);
                        if (num29 >= 2)
                        {
                            state2 = state;
                            state2.UpdateRep();
                            num30 = (position + num35) & this._posStateMask;
                            num37 = ((num11 + this.GetRepPrice(i, num35, state, posState)) + this._isMatch[(state2.Index << 4) + num30].GetPrice0()) + this._literalEncoder.GetSubCoder(position + num35, this._matchFinder.GetIndexByte((((int) num35) - 1) - 1)).GetPrice(true, this._matchFinder.GetIndexByte((int) ((num35 - 1) - (this.reps[i] + 1))), this._matchFinder.GetIndexByte(((int) num35) - 1));
                            state2.UpdateChar();
                            num30 = ((position + num35) + 1) & this._posStateMask;
                            num38 = num37 + this._isMatch[(state2.Index << 4) + num30].GetPrice1();
                            num31 = num38 + this._isRep[state2.Index].GetPrice1();
                            num32 = (num35 + 1) + num29;
                            while (num13 < (cur + num32))
                            {
                                this._optimum[(int) ((IntPtr) (++num13))].Price = 0xfffffff;
                            }
                            num17 = num31 + this.GetRepPrice(0, num29, state2, num30);
                            optimal = this._optimum[cur + num32];
                            if (num17 < optimal.Price)
                            {
                                optimal.Price = num17;
                                optimal.PosPrev = (cur + num35) + 1;
                                optimal.BackPrev = 0;
                                optimal.Prev1IsChar = true;
                                optimal.Prev2 = true;
                                optimal.PosPrev2 = cur;
                                optimal.BackPrev2 = i;
                            }
                        }
                    }
                }
            }
            if (num22 > limit)
            {
                num22 = limit;
                num3 = 0;
                while (num22 > this._matchDistances[num3])
                {
                    num3 += 2;
                }
                this._matchDistances[num3] = num22;
                num3 += 2;
            }
            if (num22 < num33)
            {
                goto Label_133B;
            }
            num18 = num10 + this._isRep[state.Index].GetPrice0();
            while (num13 < (cur + num22))
            {
                this._optimum[(int) ((IntPtr) (++num13))].Price = 0xfffffff;
            }
            for (num19 = 0; num33 > this._matchDistances[num19]; num19 += 2)
            {
            }
            num35 = num33;
            while (true)
            {
                flag2 = true;
                uint num39 = this._matchDistances[(int) ((IntPtr) (num19 + 1))];
                num17 = num18 + this.GetPosLenPrice(num39, num35, posState);
                optimal = this._optimum[cur + num35];
                if (num17 < optimal.Price)
                {
                    optimal.Price = num17;
                    optimal.PosPrev = cur;
                    optimal.BackPrev = num39 + 4;
                    optimal.Prev1IsChar = false;
                }
                if (num35 == this._matchDistances[num19])
                {
                    if (num35 < num27)
                    {
                        num28 = Math.Min((num27 - 1) - num35, this._numFastBytes);
                        num29 = this._matchFinder.GetMatchLen((int) num35, num39, num28);
                        if (num29 >= 2)
                        {
                            state2 = state;
                            state2.UpdateMatch();
                            num30 = (position + num35) & this._posStateMask;
                            num37 = (num17 + this._isMatch[(state2.Index << 4) + num30].GetPrice0()) + this._literalEncoder.GetSubCoder(position + num35, this._matchFinder.GetIndexByte((((int) num35) - 1) - 1)).GetPrice(true, this._matchFinder.GetIndexByte(((int) (num35 - (num39 + 1))) - 1), this._matchFinder.GetIndexByte(((int) num35) - 1));
                            state2.UpdateChar();
                            num30 = ((position + num35) + 1) & this._posStateMask;
                            num38 = num37 + this._isMatch[(state2.Index << 4) + num30].GetPrice1();
                            num31 = num38 + this._isRep[state2.Index].GetPrice1();
                            num32 = (num35 + 1) + num29;
                            while (num13 < (cur + num32))
                            {
                                this._optimum[(int) ((IntPtr) (++num13))].Price = 0xfffffff;
                            }
                            num17 = num31 + this.GetRepPrice(0, num29, state2, num30);
                            optimal = this._optimum[cur + num32];
                            if (num17 < optimal.Price)
                            {
                                optimal.Price = num17;
                                optimal.PosPrev = (cur + num35) + 1;
                                optimal.BackPrev = 0;
                                optimal.Prev1IsChar = true;
                                optimal.Prev2 = true;
                                optimal.PosPrev2 = cur;
                                optimal.BackPrev2 = num39 + 4;
                            }
                        }
                    }
                    num19 += 2;
                    if (num19 == num3)
                    {
                        goto Label_133B;
                    }
                }
                num35++;
            }
        }

        private uint GetPosLenPrice(uint pos, uint len, uint posState)
        {
            uint num;
            uint lenToPosState = Base.GetLenToPosState(len);
            if (pos < 0x80)
            {
                num = this._distancesPrices[(int) ((IntPtr) ((lenToPosState * 0x80) + pos))];
            }
            else
            {
                num = this._posSlotPrices[(lenToPosState << 6) + GetPosSlot2(pos)] + this._alignPrices[(int) ((IntPtr) (pos & 15))];
            }
            return (num + this._lenEncoder.GetPrice(len - 2, posState));
        }

        private static uint GetPosSlot(uint pos)
        {
            if (pos < 0x800)
            {
                return g_FastPos[pos];
            }
            if (pos < 0x200000)
            {
                return (uint) (g_FastPos[pos >> 10] + 20);
            }
            return (uint) (g_FastPos[pos >> 20] + 40);
        }

        private static uint GetPosSlot2(uint pos)
        {
            if (pos < 0x20000)
            {
                return (uint) (g_FastPos[pos >> 6] + 12);
            }
            if (pos < 0x8000000)
            {
                return (uint) (g_FastPos[pos >> 0x10] + 0x20);
            }
            return (uint) (g_FastPos[pos >> 0x1a] + 0x34);
        }

        private uint GetPureRepPrice(uint repIndex, Base.State state, uint posState)
        {
            if (repIndex == 0)
            {
                return (this._isRepG0[state.Index].GetPrice0() + this._isRep0Long[(state.Index << 4) + posState].GetPrice1());
            }
            uint num = this._isRepG0[state.Index].GetPrice1();
            if (repIndex == 1)
            {
                return (num + this._isRepG1[state.Index].GetPrice0());
            }
            num += this._isRepG1[state.Index].GetPrice1();
            return (num + this._isRepG2[state.Index].GetPrice(repIndex - 2));
        }

        private uint GetRepLen1Price(Base.State state, uint posState)
        {
            return (this._isRepG0[state.Index].GetPrice0() + this._isRep0Long[(state.Index << 4) + posState].GetPrice0());
        }

        private uint GetRepPrice(uint repIndex, uint len, Base.State state, uint posState)
        {
            return (this._repMatchLenEncoder.GetPrice(len - 2, posState) + this.GetPureRepPrice(repIndex, state, posState));
        }

        private void Init()
        {
            uint num;
            this.BaseInit();
            this._rangeEncoder.Init();
            for (num = 0; num < 12; num++)
            {
                for (uint i = 0; i <= this._posStateMask; i++)
                {
                    uint index = (num << 4) + i;
                    this._isMatch[index].Init();
                    this._isRep0Long[index].Init();
                }
                this._isRep[num].Init();
                this._isRepG0[num].Init();
                this._isRepG1[num].Init();
                this._isRepG2[num].Init();
            }
            this._literalEncoder.Init();
            for (num = 0; num < 4; num++)
            {
                this._posSlotEncoder[num].Init();
            }
            for (num = 0; num < 0x72; num++)
            {
                this._posEncoders[num].Init();
            }
            this._lenEncoder.Init(((uint) 1) << this._posStateBits);
            this._repMatchLenEncoder.Init(((uint) 1) << this._posStateBits);
            this._posAlignEncoder.Init();
            this._longestMatchWasFound = false;
            this._optimumEndIndex = 0;
            this._optimumCurrentIndex = 0;
            this._additionalOffset = 0;
        }

        private void MovePos(uint num)
        {
            if (num > 0)
            {
                this._matchFinder.Skip(num);
                this._additionalOffset += num;
            }
        }

        private void ReadMatchDistances(out uint lenRes, out uint numDistancePairs)
        {
            lenRes = 0;
            numDistancePairs = this._matchFinder.GetMatches(this._matchDistances);
            if (numDistancePairs > 0)
            {
                lenRes = this._matchDistances[numDistancePairs - 2];
                if (lenRes == this._numFastBytes)
                {
                    lenRes += this._matchFinder.GetMatchLen(((int) lenRes) - 1, this._matchDistances[numDistancePairs - 1], 0x111 - lenRes);
                }
            }
            this._additionalOffset++;
        }

        private void ReleaseMFStream()
        {
            if ((this._matchFinder != null) && this._needReleaseMFStream)
            {
                this._matchFinder.ReleaseStream();
                this._needReleaseMFStream = false;
            }
        }

        private void ReleaseOutStream()
        {
            this._rangeEncoder.ReleaseStream();
        }

        private void ReleaseStreams()
        {
            this.ReleaseMFStream();
            this.ReleaseOutStream();
        }

        public void SetCoderProperties(CoderPropID[] propIDs, object[] properties)
        {
            for (uint i = 0; i < properties.Length; i++)
            {
                EMatchFinderType type;
                int num4;
                int num6;
                object obj2 = properties[i];
                switch (propIDs[i])
                {
                    case CoderPropID.DictionarySize:
                        if (!(obj2 is int))
                        {
                            throw new InvalidParamException();
                        }
                        goto Label_013B;

                    case CoderPropID.PosStateBits:
                        if (!(obj2 is int))
                        {
                            throw new InvalidParamException();
                        }
                        goto Label_01C3;

                    case CoderPropID.LitContextBits:
                        if (!(obj2 is int))
                        {
                            throw new InvalidParamException();
                        }
                        goto Label_0267;

                    case CoderPropID.LitPosBits:
                        if (!(obj2 is int))
                        {
                            throw new InvalidParamException();
                        }
                        goto Label_0220;

                    case CoderPropID.NumFastBytes:
                        if (!(obj2 is int))
                        {
                            throw new InvalidParamException();
                        }
                        break;

                    case CoderPropID.MatchFinder:
                        if (!(obj2 is string))
                        {
                            throw new InvalidParamException();
                        }
                        goto Label_00C0;

                    case CoderPropID.Algorithm:
                    {
                        continue;
                    }
                    case CoderPropID.EndMarker:
                        if (!(obj2 is bool))
                        {
                            throw new InvalidParamException();
                        }
                        goto Label_02AE;

                    default:
                        throw new InvalidParamException();
                }
                int num2 = (int) obj2;
                if ((num2 < 5) || (num2 > 0x111L))
                {
                    throw new InvalidParamException();
                }
                this._numFastBytes = (uint) num2;
                continue;
            Label_00C0:
                type = this._matchFinderType;
                int num3 = FindMatchFinder(((string) obj2).ToUpper());
                if (num3 < 0)
                {
                    throw new InvalidParamException();
                }
                this._matchFinderType = (EMatchFinderType) num3;
                if ((this._matchFinder != null) && (type != this._matchFinderType))
                {
                    this._dictionarySizePrev = uint.MaxValue;
                    this._matchFinder = null;
                }
                continue;
            Label_013B:
                num4 = (int) obj2;
                if ((num4 < 1L) || (num4 > 0x40000000L))
                {
                    throw new InvalidParamException();
                }
                this._dictionarySize = (uint) num4;
                int num5 = 0;
                while (num5 < 30L)
                {
                    if (num4 <= (((uint) 1) << num5))
                    {
                        break;
                    }
                    num5++;
                }
                this._distTableSize = (uint) (num5 * 2);
                continue;
            Label_01C3:
                num6 = (int) obj2;
                if ((num6 < 0) || (num6 > 4L))
                {
                    throw new InvalidParamException();
                }
                this._posStateBits = num6;
                this._posStateMask = (uint) ((((int) 1) << this._posStateBits) - 1);
                continue;
            Label_0220:
                num6 = (int) obj2;
                if ((num6 < 0) || (num6 > 4L))
                {
                    throw new InvalidParamException();
                }
                this._numLiteralPosStateBits = num6;
                continue;
            Label_0267:
                num6 = (int) obj2;
                if ((num6 < 0) || (num6 > 8L))
                {
                    throw new InvalidParamException();
                }
                this._numLiteralContextBits = num6;
                continue;
            Label_02AE:
                this.SetWriteEndMarkerMode((bool) obj2);
            }
        }

        private void SetOutStream(Stream outStream)
        {
            this._rangeEncoder.SetStream(outStream);
        }

        public void SetStreams(Stream inStream, Stream outStream, long inSize, long outSize)
        {
            this._inStream = inStream;
            this._finished = false;
            this.Create();
            this.SetOutStream(outStream);
            this.Init();
            this._matchFinder.Init();
            this.FillDistancesPrices();
            this.FillAlignPrices();
            this._lenEncoder.SetTableSize((this._numFastBytes + 1) - 2);
            this._lenEncoder.UpdateTables(((uint) 1) << this._posStateBits);
            this._repMatchLenEncoder.SetTableSize((this._numFastBytes + 1) - 2);
            this._repMatchLenEncoder.UpdateTables(((uint) 1) << this._posStateBits);
            this.nowPos64 = 0L;
        }

        public void SetTrainSize(uint trainSize)
        {
            this._trainSize = trainSize;
        }

        private void SetWriteEndMarkerMode(bool writeEndMarker)
        {
            this._writeEndMark = writeEndMarker;
        }

        public void Train(Stream trainStream)
        {
            if (this.nowPos64 > 0L)
            {
                throw new InvalidOperationException();
            }
            this._trainSize = (uint) trainStream.Length;
            if (this._trainSize > 0)
            {
                this._matchFinder.SetStream(trainStream);
                while ((this._trainSize > 0) && !this._matchFinder.IsDataStarved)
                {
                    this._matchFinder.Skip(1);
                    this._trainSize--;
                }
                if (this._trainSize == 0)
                {
                    this._previousByte = this._matchFinder.GetIndexByte(-1);
                }
                this._matchFinder.ReleaseStream();
            }
        }

        public void WriteCoderProperties(Stream outStream)
        {
            this.properties[0] = (byte) ((((this._posStateBits * 5) + this._numLiteralPosStateBits) * 9) + this._numLiteralContextBits);
            for (int i = 0; i < 4; i++)
            {
                this.properties[1 + i] = (byte) ((this._dictionarySize >> (8 * i)) & 0xff);
            }
            outStream.Write(this.properties, 0, 5);
        }

        private void WriteEndMarker(uint posState)
        {
            if (this._writeEndMark)
            {
                this._isMatch[(this._state.Index << 4) + posState].Encode(this._rangeEncoder, 1);
                this._isRep[this._state.Index].Encode(this._rangeEncoder, 0);
                this._state.UpdateMatch();
                uint len = 2;
                this._lenEncoder.Encode(this._rangeEncoder, len - 2, posState);
                uint symbol = 0x3f;
                uint lenToPosState = Base.GetLenToPosState(len);
                this._posSlotEncoder[lenToPosState].Encode(this._rangeEncoder, symbol);
                int num4 = 30;
                uint num5 = (uint) ((((int) 1) << num4) - 1);
                this._rangeEncoder.EncodeDirectBits(num5 >> 4, num4 - 4);
                this._posAlignEncoder.ReverseEncode(this._rangeEncoder, num5 & 15);
            }
        }

        private enum EMatchFinderType
        {
            BT2,
            BT4
        }

        private class LenEncoder
        {
            private BitEncoder _choice = new BitEncoder();
            private BitEncoder _choice2 = new BitEncoder();
            private BitTreeEncoder _highCoder = new BitTreeEncoder(8);
            private BitTreeEncoder[] _lowCoder = new BitTreeEncoder[0x10];
            private BitTreeEncoder[] _midCoder = new BitTreeEncoder[0x10];

            public LenEncoder()
            {
                for (uint i = 0; i < 0x10; i++)
                {
                    this._lowCoder[i] = new BitTreeEncoder(3);
                    this._midCoder[i] = new BitTreeEncoder(3);
                }
            }

            public void Encode(SharpCompress.Compressor.LZMA.RangeCoder.Encoder rangeEncoder, uint symbol, uint posState)
            {
                if (symbol < 8)
                {
                    this._choice.Encode(rangeEncoder, 0);
                    this._lowCoder[posState].Encode(rangeEncoder, symbol);
                }
                else
                {
                    symbol -= 8;
                    this._choice.Encode(rangeEncoder, 1);
                    if (symbol < 8)
                    {
                        this._choice2.Encode(rangeEncoder, 0);
                        this._midCoder[posState].Encode(rangeEncoder, symbol);
                    }
                    else
                    {
                        this._choice2.Encode(rangeEncoder, 1);
                        this._highCoder.Encode(rangeEncoder, symbol - 8);
                    }
                }
            }

            public void Init(uint numPosStates)
            {
                this._choice.Init();
                this._choice2.Init();
                for (uint i = 0; i < numPosStates; i++)
                {
                    this._lowCoder[i].Init();
                    this._midCoder[i].Init();
                }
                this._highCoder.Init();
            }

            public void SetPrices(uint posState, uint numSymbols, uint[] prices, uint st)
            {
                uint num = this._choice.GetPrice0();
                uint num2 = this._choice.GetPrice1();
                uint num3 = num2 + this._choice2.GetPrice0();
                uint num4 = num2 + this._choice2.GetPrice1();
                uint symbol = 0;
                symbol = 0;
                while (symbol < 8)
                {
                    if (symbol >= numSymbols)
                    {
                        return;
                    }
                    prices[st + symbol] = num + this._lowCoder[posState].GetPrice(symbol);
                    symbol++;
                }
                while (symbol < 0x10)
                {
                    if (symbol >= numSymbols)
                    {
                        return;
                    }
                    prices[st + symbol] = num3 + this._midCoder[posState].GetPrice(symbol - 8);
                    symbol++;
                }
                while (symbol < numSymbols)
                {
                    prices[st + symbol] = num4 + this._highCoder.GetPrice((symbol - 8) - 8);
                    symbol++;
                }
            }
        }

        private class LenPriceTableEncoder : SharpCompress.Compressor.LZMA.Encoder.LenEncoder
        {
            private uint[] _counters = new uint[0x10];
            private uint[] _prices = new uint[0x1100];
            private uint _tableSize;

            public void Encode(SharpCompress.Compressor.LZMA.RangeCoder.Encoder rangeEncoder, uint symbol, uint posState)
            {
                base.Encode(rangeEncoder, symbol, posState);
                if ((this._counters[posState] -= 1) == 0)
                {
                    this.UpdateTable(posState);
                }
            }

            public uint GetPrice(uint symbol, uint posState)
            {
                return this._prices[(int) ((IntPtr) ((posState * 0x110) + symbol))];
            }

            public void SetTableSize(uint tableSize)
            {
                this._tableSize = tableSize;
            }

            private void UpdateTable(uint posState)
            {
                base.SetPrices(posState, this._tableSize, this._prices, posState * 0x110);
                this._counters[posState] = this._tableSize;
            }

            public void UpdateTables(uint numPosStates)
            {
                for (uint i = 0; i < numPosStates; i++)
                {
                    this.UpdateTable(i);
                }
            }
        }

        private class LiteralEncoder
        {
            private Encoder2[] m_Coders;
            private int m_NumPosBits;
            private int m_NumPrevBits;
            private uint m_PosMask;

            public void Create(int numPosBits, int numPrevBits)
            {
                if (((this.m_Coders == null) || (this.m_NumPrevBits != numPrevBits)) || (this.m_NumPosBits != numPosBits))
                {
                    this.m_NumPosBits = numPosBits;
                    this.m_PosMask = (uint) ((((int) 1) << numPosBits) - 1);
                    this.m_NumPrevBits = numPrevBits;
                    uint num = ((uint) 1) << (this.m_NumPrevBits + this.m_NumPosBits);
                    this.m_Coders = new Encoder2[num];
                    for (uint i = 0; i < num; i++)
                    {
                        this.m_Coders[i].Create();
                    }
                }
            }

            public Encoder2 GetSubCoder(uint pos, byte prevByte)
            {
                return this.m_Coders[(int) ((IntPtr) (((pos & this.m_PosMask) << this.m_NumPrevBits) + (prevByte >> (8 - this.m_NumPrevBits))))];
            }

            public void Init()
            {
                uint num = ((uint) 1) << (this.m_NumPrevBits + this.m_NumPosBits);
                for (uint i = 0; i < num; i++)
                {
                    this.m_Coders[i].Init();
                }
            }

            [StructLayout(LayoutKind.Sequential)]
            public struct Encoder2
            {
                private BitEncoder[] m_Encoders;
                public void Create()
                {
                    this.m_Encoders = new BitEncoder[0x300];
                }

                public void Init()
                {
                    for (int i = 0; i < 0x300; i++)
                    {
                        this.m_Encoders[i].Init();
                    }
                }

                public void Encode(SharpCompress.Compressor.LZMA.RangeCoder.Encoder rangeEncoder, byte symbol)
                {
                    uint index = 1;
                    for (int i = 7; i >= 0; i--)
                    {
                        uint num3 = (uint) ((symbol >> i) & 1);
                        this.m_Encoders[index].Encode(rangeEncoder, num3);
                        index = (index << 1) | num3;
                    }
                }

                public void EncodeMatched(SharpCompress.Compressor.LZMA.RangeCoder.Encoder rangeEncoder, byte matchByte, byte symbol)
                {
                    uint num = 1;
                    bool flag = true;
                    for (int i = 7; i >= 0; i--)
                    {
                        uint num3 = (uint) ((symbol >> i) & 1);
                        uint index = num;
                        if (flag)
                        {
                            uint num5 = (uint) ((matchByte >> i) & 1);
                            index += (uint) ((1 + num5) << 8);
                            flag = num5 == num3;
                        }
                        this.m_Encoders[index].Encode(rangeEncoder, num3);
                        num = (num << 1) | num3;
                    }
                }

                public uint GetPrice(bool matchMode, byte matchByte, byte symbol)
                {
                    uint num5;
                    uint num = 0;
                    uint index = 1;
                    int num3 = 7;
                    if (matchMode)
                    {
                        while (num3 >= 0)
                        {
                            uint num4 = (uint) ((matchByte >> num3) & 1);
                            num5 = (uint) ((symbol >> num3) & 1);
                            num += this.m_Encoders[(int) ((IntPtr) (((1 + num4) << 8) + index))].GetPrice(num5);
                            index = (index << 1) | num5;
                            if (num4 != num5)
                            {
                                num3--;
                                break;
                            }
                            num3--;
                        }
                    }
                    while (num3 >= 0)
                    {
                        num5 = (uint) ((symbol >> num3) & 1);
                        num += this.m_Encoders[index].GetPrice(num5);
                        index = (index << 1) | num5;
                        num3--;
                    }
                    return num;
                }
            }
        }

        private class Optimal
        {
            public uint BackPrev;
            public uint BackPrev2;
            public uint Backs0;
            public uint Backs1;
            public uint Backs2;
            public uint Backs3;
            public uint PosPrev;
            public uint PosPrev2;
            public bool Prev1IsChar;
            public bool Prev2;
            public uint Price;
            public SharpCompress.Compressor.LZMA.Base.State State;

            public bool IsShortRep()
            {
                return (this.BackPrev == 0);
            }

            public void MakeAsChar()
            {
                this.BackPrev = uint.MaxValue;
                this.Prev1IsChar = false;
            }

            public void MakeAsShortRep()
            {
                this.BackPrev = 0;
                this.Prev1IsChar = false;
            }
        }
    }
}

