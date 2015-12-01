namespace SharpCompress.Compressor.LZMA
{
    using System;
    using System.Runtime.InteropServices;

    internal abstract class Base
    {
        public const uint kAlignMask = 15;
        public const uint kAlignTableSize = 0x10;
        public const int kDicLogSizeMin = 0;
        public const uint kEndPosModelIndex = 14;
        public const uint kMatchMaxLen = 0x111;
        public const uint kMatchMinLen = 2;
        public const int kNumAlignBits = 4;
        public const uint kNumFullDistances = 0x80;
        public const int kNumHighLenBits = 8;
        public const uint kNumLenSymbols = 0x110;
        public const uint kNumLenToPosStates = 4;
        public const int kNumLenToPosStatesBits = 2;
        public const uint kNumLitContextBitsMax = 8;
        public const uint kNumLitPosStatesBitsEncodingMax = 4;
        public const int kNumLowLenBits = 3;
        public const uint kNumLowLenSymbols = 8;
        public const int kNumMidLenBits = 3;
        public const uint kNumMidLenSymbols = 8;
        public const uint kNumPosModels = 10;
        public const int kNumPosSlotBits = 6;
        public const int kNumPosStatesBitsEncodingMax = 4;
        public const int kNumPosStatesBitsMax = 4;
        public const uint kNumPosStatesEncodingMax = 0x10;
        public const uint kNumPosStatesMax = 0x10;
        public const uint kNumRepDistances = 4;
        public const uint kNumStates = 12;
        public const uint kStartPosModelIndex = 4;

        protected Base()
        {
        }

        public static uint GetLenToPosState(uint len)
        {
            len -= 2;
            if (len < 4)
            {
                return len;
            }
            return 3;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct State
        {
            public uint Index;
            public void Init()
            {
                this.Index = 0;
            }

            public void UpdateChar()
            {
                if (this.Index < 4)
                {
                    this.Index = 0;
                }
                else if (this.Index < 10)
                {
                    this.Index -= 3;
                }
                else
                {
                    this.Index -= 6;
                }
            }

            public void UpdateMatch()
            {
                this.Index = (uint)((this.Index < 7) ? 7 : 10);
            }

            public void UpdateRep()
            {
                this.Index = (uint)((this.Index < 7) ? 8 : 11);
            }

            public void UpdateShortRep()
            {
                this.Index = (uint)((this.Index < 7) ? 9 : 11);
            }

            public bool IsCharState()
            {
                return (this.Index < 7);
            }
        }
    }
}

