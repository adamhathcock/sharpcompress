using System;
using System.Buffers.Binary;

namespace SharpCompress.Common.Dmg.Headers
{
    internal abstract class DmgStructBase
    {
        protected static uint ReadUInt32(ref ReadOnlySpan<byte> data)
        {
            uint val = BinaryPrimitives.ReadUInt32BigEndian(data);
            data = data.Slice(sizeof(uint));
            return val;
        }

        protected static ulong ReadUInt64(ref ReadOnlySpan<byte> data)
        {
            ulong val = BinaryPrimitives.ReadUInt64BigEndian(data);
            data = data.Slice(sizeof(ulong));
            return val;
        }
    }
}
