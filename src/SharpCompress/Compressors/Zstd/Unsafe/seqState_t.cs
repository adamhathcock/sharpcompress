using InlineIL;
using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using static InlineIL.IL.Emit;

namespace ZstdSharp.Unsafe
{
    public partial struct seqState_t
    {
        public BIT_DStream_t DStream;

        public ZSTD_fseState stateLL;

        public ZSTD_fseState stateOffb;

        public ZSTD_fseState stateML;

        public _prevOffset_e__FixedBuffer prevOffset;

        public unsafe partial struct _prevOffset_e__FixedBuffer
        {
            public nuint e0;
            public nuint e1;
            public nuint e2;

            public ref nuint this[int index]
            {
                [MethodImpl(MethodImplOptions.AggressiveInlining)]
                [InlineMethod.Inline]
                get => ref *(this + (uint)index);
            }

            public ref nuint this[uint index]
            {
                [MethodImpl(MethodImplOptions.AggressiveInlining)]
                [InlineMethod.Inline]
                get => ref *(this + index);
            }

            public ref nuint this[nuint index]
            {
                [MethodImpl(MethodImplOptions.AggressiveInlining)]
                [InlineMethod.Inline]
                get => ref *(this + (uint)index);
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            [InlineMethod.Inline]
            public static implicit operator nuint*(in _prevOffset_e__FixedBuffer t)
            {
                Ldarg_0();
                Ldflda(new FieldRef(typeof(_prevOffset_e__FixedBuffer), nameof(e0)));
                return IL.ReturnPointer<nuint>();
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            [InlineMethod.Inline]
            public static nuint* operator +(in _prevOffset_e__FixedBuffer t, uint index)
            {
                Ldarg_0();
                Ldflda(new FieldRef(typeof(_prevOffset_e__FixedBuffer), nameof(e0)));
                Ldarg_1();
                Conv_I();
                Sizeof<nuint>();
                Conv_I();
                Mul();
                Add();
                return IL.ReturnPointer<nuint>();
            }
        }
    }
}
