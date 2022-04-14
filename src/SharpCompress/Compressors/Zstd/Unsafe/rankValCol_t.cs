using InlineIL;
using System;
using System.Runtime.CompilerServices;
using static InlineIL.IL.Emit;

namespace ZstdSharp.Unsafe
{
    public unsafe partial struct rankValCol_t
    {
        public fixed uint Body[13];

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        [InlineMethod.Inline]
        public static implicit operator uint*(in rankValCol_t t)
        {
            Ldarg_0();
            Ldflda(new FieldRef(typeof(rankValCol_t), nameof(Body)));
            return IL.ReturnPointer<uint>();
        }
    }
}
