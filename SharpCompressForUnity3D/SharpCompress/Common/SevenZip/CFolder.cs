namespace SharpCompress.Common.SevenZip
{
    using SharpCompress.Compressor.LZMA;
    using System;
    using System.Collections.Generic;

    internal class CFolder
    {
        internal List<CBindPair> BindPairs = new List<CBindPair>();
        internal List<CCoderInfo> Coders = new List<CCoderInfo>();
        internal int FirstPackStreamId;
        internal List<int> PackStreams = new List<int>();
        internal uint? UnpackCRC;
        internal List<long> UnpackSizes = new List<long>();

        public bool CheckStructure()
        {
            int num;
            int num2;
            if ((this.Coders.Count > 0x20) || (this.BindPairs.Count > 0x20))
            {
                return false;
            }
            BitVector vector = new BitVector(this.BindPairs.Count + this.PackStreams.Count);
            for (num = 0; num < this.BindPairs.Count; num++)
            {
                if (vector.GetAndSet(this.BindPairs[num].InIndex))
                {
                    return false;
                }
            }
            for (num = 0; num < this.PackStreams.Count; num++)
            {
                if (vector.GetAndSet(this.PackStreams[num]))
                {
                    return false;
                }
            }
            vector = new BitVector(this.UnpackSizes.Count);
            for (num = 0; num < this.BindPairs.Count; num++)
            {
                if (vector.GetAndSet(this.BindPairs[num].OutIndex))
                {
                    return false;
                }
            }
            uint[] numArray = new uint[0x20];
            List<int> list = new List<int>();
            List<int> list2 = new List<int>();
            for (num = 0; num < this.Coders.Count; num++)
            {
                CCoderInfo info = this.Coders[num];
                num2 = 0;
                while (num2 < info.NumInStreams)
                {
                    list.Add(num);
                    num2++;
                }
                num2 = 0;
                while (num2 < info.NumOutStreams)
                {
                    list2.Add(num);
                    num2++;
                }
            }
            for (num = 0; num < this.BindPairs.Count; num++)
            {
                CBindPair pair = this.BindPairs[num];
                numArray[list[pair.InIndex]] |= ((uint) 1) << list2[pair.OutIndex];
            }
            for (num = 0; num < 0x20; num++)
            {
                for (num2 = 0; num2 < 0x20; num2++)
                {
                    if (((((int) 1) << num2) & numArray[num]) != 0)
                    {
                        numArray[num] |= numArray[num2];
                    }
                }
            }
            for (num = 0; num < 0x20; num++)
            {
                if (((((int) 1) << num) & numArray[num]) != 0)
                {
                    return false;
                }
            }
            return true;
        }

        public int FindBindPairForInStream(int inStreamIndex)
        {
            for (int i = 0; i < this.BindPairs.Count; i++)
            {
                if (this.BindPairs[i].InIndex == inStreamIndex)
                {
                    return i;
                }
            }
            return -1;
        }

        public int FindBindPairForOutStream(int outStreamIndex)
        {
            for (int i = 0; i < this.BindPairs.Count; i++)
            {
                if (this.BindPairs[i].OutIndex == outStreamIndex)
                {
                    return i;
                }
            }
            return -1;
        }

        public int FindPackStreamArrayIndex(int inStreamIndex)
        {
            for (int i = 0; i < this.PackStreams.Count; i++)
            {
                if (this.PackStreams[i] == inStreamIndex)
                {
                    return i;
                }
            }
            return -1;
        }

        public int GetNumOutStreams()
        {
            int num = 0;
            for (int i = 0; i < this.Coders.Count; i++)
            {
                num += this.Coders[i].NumOutStreams;
            }
            return num;
        }

        public long GetUnpackSize()
        {
            if (this.UnpackSizes.Count == 0)
            {
                return 0L;
            }
            for (int i = this.UnpackSizes.Count - 1; i >= 0; i--)
            {
                if (this.FindBindPairForOutStream(i) < 0)
                {
                    return this.UnpackSizes[i];
                }
            }
            throw new Exception();
        }

        public bool IsEncrypted()
        {
            for (int i = this.Coders.Count - 1; i >= 0; i--)
            {
                if (this.Coders[i].MethodId == CMethodId.kAES)
                {
                    return true;
                }
            }
            return false;
        }

        internal bool UnpackCRCDefined
        {
            get
            {
                return this.UnpackCRC.HasValue;
            }
        }
    }
}

