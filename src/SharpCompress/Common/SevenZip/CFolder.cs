using System;
using System.Collections.Generic;
using SharpCompress.Compressor.LZMA;

namespace SharpCompress.Common.SevenZip
{
    internal class CFolder
    {
        internal List<CCoderInfo> Coders = new List<CCoderInfo>();
        internal List<CBindPair> BindPairs = new List<CBindPair>();
        internal List<int> PackStreams = new List<int>();
        internal int FirstPackStreamId;
        internal List<long> UnpackSizes = new List<long>();
        internal uint? UnpackCRC;

        internal bool UnpackCRCDefined
        {
            get { return UnpackCRC != null; }
        }

        public long GetUnpackSize()
        {
            if (UnpackSizes.Count == 0)
                return 0;

            for (int i = UnpackSizes.Count - 1; i >= 0; i--)
                if (FindBindPairForOutStream(i) < 0)
                    return UnpackSizes[i];

            throw new Exception();
        }

        public int GetNumOutStreams()
        {
            int count = 0;
            for (int i = 0; i < Coders.Count; i++)
                count += Coders[i].NumOutStreams;

            return count;
        }

        public int FindBindPairForInStream(int inStreamIndex)
        {
            for (int i = 0; i < BindPairs.Count; i++)
                if (BindPairs[i].InIndex == inStreamIndex)
                    return i;

            return -1;
        }

        public int FindBindPairForOutStream(int outStreamIndex)
        {
            for (int i = 0; i < BindPairs.Count; i++)
                if (BindPairs[i].OutIndex == outStreamIndex)
                    return i;

            return -1;
        }

        public int FindPackStreamArrayIndex(int inStreamIndex)
        {
            for (int i = 0; i < PackStreams.Count; i++)
                if (PackStreams[i] == inStreamIndex)
                    return i;

            return -1;
        }

        public bool IsEncrypted()
        {
            for (int i = Coders.Count - 1; i >= 0; i--)
                if (Coders[i].MethodId == CMethodId.kAES)
                    return true;

            return false;
        }

        public bool CheckStructure()
        {
            const int kNumCodersMax = 32; // don't change it
            const int kMaskSize = 32; // it must be >= kNumCodersMax
            const int kNumBindsMax = 32;

            if (Coders.Count > kNumCodersMax || BindPairs.Count > kNumBindsMax)
                return false;

            {
                var v = new BitVector(BindPairs.Count + PackStreams.Count);

                for (int i = 0; i < BindPairs.Count; i++)
                    if (v.GetAndSet(BindPairs[i].InIndex))
                        return false;

                for (int i = 0; i < PackStreams.Count; i++)
                    if (v.GetAndSet(PackStreams[i]))
                        return false;
            }

            {
                var v = new BitVector(UnpackSizes.Count);
                for (int i = 0; i < BindPairs.Count; i++)
                    if (v.GetAndSet(BindPairs[i].OutIndex))
                        return false;
            }

            uint[] mask = new uint[kMaskSize];

            {
                List<int> inStreamToCoder = new List<int>();
                List<int> outStreamToCoder = new List<int>();
                for (int i = 0; i < Coders.Count; i++)
                {
                    CCoderInfo coder = Coders[i];
                    for (int j = 0; j < coder.NumInStreams; j++)
                        inStreamToCoder.Add(i);
                    for (int j = 0; j < coder.NumOutStreams; j++)
                        outStreamToCoder.Add(i);
                }

                for (int i = 0; i < BindPairs.Count; i++)
                {
                    CBindPair bp = BindPairs[i];
                    mask[inStreamToCoder[bp.InIndex]] |= (1u << outStreamToCoder[bp.OutIndex]);
                }
            }

            for (int i = 0; i < kMaskSize; i++)
                for (int j = 0; j < kMaskSize; j++)
                    if (((1u << j) & mask[i]) != 0)
                        mask[i] |= mask[j];

            for (int i = 0; i < kMaskSize; i++)
                if (((1u << i) & mask[i]) != 0)
                    return false;

            return true;
        }
    }
}