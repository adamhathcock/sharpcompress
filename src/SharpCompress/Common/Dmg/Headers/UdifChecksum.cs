using System;
using System.Collections.Generic;

namespace SharpCompress.Common.Dmg.Headers
{
    internal sealed class UdifChecksum : DmgStructBase
    {
        private const int MaxSize = 32; // * 4 to get byte size

        public uint Type { get; }
        public uint Size { get; } // in bits
        public IReadOnlyList<uint> Bits { get; }

        private UdifChecksum(uint type, uint size, IReadOnlyList<uint> bits)
        {
            Type = type;
            Size = size;
            Bits = bits;
        }

        public static UdifChecksum Read(ref ReadOnlySpan<byte> data)
        {
            uint type = ReadUInt32(ref data);
            uint size = ReadUInt32(ref data);

            var bits = new uint[MaxSize];
            for (int i = 0; i < MaxSize; i++)
                bits[i] = ReadUInt32(ref data);

            return new UdifChecksum(type, size, bits);
        }
    }
}
