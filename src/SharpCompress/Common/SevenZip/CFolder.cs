using System;
using System.Collections.Generic;
using SharpCompress.Compressors.LZMA;

namespace SharpCompress.Common.SevenZip;

internal class CFolder
{
    internal List<CCoderInfo> _coders = new();
    internal List<CBindPair> _bindPairs = new();
    internal List<int> _packStreams = new();
    internal int _firstPackStreamId;
    internal List<long> _unpackSizes = new();
    internal uint? _unpackCrc;

    internal bool UnpackCrcDefined => _unpackCrc != null;

    public long GetUnpackSize()
    {
        if (_unpackSizes.Count == 0)
        {
            return 0;
        }

        for (var i = _unpackSizes.Count - 1; i >= 0; i--)
        {
            if (FindBindPairForOutStream(i) < 0)
            {
                return _unpackSizes[i];
            }
        }

        throw new InvalidOperationException();
    }

    public int GetNumOutStreams()
    {
        var count = 0;
        for (var i = 0; i < _coders.Count; i++)
        {
            count += _coders[i]._numOutStreams;
        }

        return count;
    }

    public int FindBindPairForInStream(int inStreamIndex)
    {
        for (var i = 0; i < _bindPairs.Count; i++)
        {
            if (_bindPairs[i]._inIndex == inStreamIndex)
            {
                return i;
            }
        }

        return -1;
    }

    public int FindBindPairForOutStream(int outStreamIndex)
    {
        for (var i = 0; i < _bindPairs.Count; i++)
        {
            if (_bindPairs[i]._outIndex == outStreamIndex)
            {
                return i;
            }
        }

        return -1;
    }

    public int FindPackStreamArrayIndex(int inStreamIndex)
    {
        for (var i = 0; i < _packStreams.Count; i++)
        {
            if (_packStreams[i] == inStreamIndex)
            {
                return i;
            }
        }

        return -1;
    }

    public bool IsEncrypted()
    {
        for (var i = _coders.Count - 1; i >= 0; i--)
        {
            if (_coders[i]._methodId == CMethodId.K_AES)
            {
                return true;
            }
        }

        return false;
    }

    public bool CheckStructure()
    {
        const int kNumCodersMax = 32; // don't change it
        const int kMaskSize = 32; // it must be >= kNumCodersMax
        const int kNumBindsMax = 32;

        if (_coders.Count > kNumCodersMax || _bindPairs.Count > kNumBindsMax)
        {
            return false;
        }

        {
            var v = new BitVector(_bindPairs.Count + _packStreams.Count);

            for (var i = 0; i < _bindPairs.Count; i++)
            {
                if (v.GetAndSet(_bindPairs[i]._inIndex))
                {
                    return false;
                }
            }

            for (var i = 0; i < _packStreams.Count; i++)
            {
                if (v.GetAndSet(_packStreams[i]))
                {
                    return false;
                }
            }
        }

        {
            var v = new BitVector(_unpackSizes.Count);
            for (var i = 0; i < _bindPairs.Count; i++)
            {
                if (v.GetAndSet(_bindPairs[i]._outIndex))
                {
                    return false;
                }
            }
        }

        var mask = new uint[kMaskSize];

        {
            var inStreamToCoder = new List<int>();
            var outStreamToCoder = new List<int>();
            for (var i = 0; i < _coders.Count; i++)
            {
                var coder = _coders[i];
                for (var j = 0; j < coder._numInStreams; j++)
                {
                    inStreamToCoder.Add(i);
                }
                for (var j = 0; j < coder._numOutStreams; j++)
                {
                    outStreamToCoder.Add(i);
                }
            }

            for (var i = 0; i < _bindPairs.Count; i++)
            {
                var bp = _bindPairs[i];
                mask[inStreamToCoder[bp._inIndex]] |= (1u << outStreamToCoder[bp._outIndex]);
            }
        }

        for (var i = 0; i < kMaskSize; i++)
        {
            for (var j = 0; j < kMaskSize; j++)
            {
                if (((1u << j) & mask[i]) != 0)
                {
                    mask[i] |= mask[j];
                }
            }
        }

        for (var i = 0; i < kMaskSize; i++)
        {
            if (((1u << i) & mask[i]) != 0)
            {
                return false;
            }
        }

        return true;
    }
}
