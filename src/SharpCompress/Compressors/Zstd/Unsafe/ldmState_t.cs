using InlineIL;
using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using static InlineIL.IL.Emit;

namespace ZstdSharp.Unsafe
{
    public unsafe partial struct ldmState_t
    {
        /* State for the window round buffer management */
        public ZSTD_window_t window;

        public ldmEntry_t* hashTable;

        public uint loadedDictEnd;

        /* Next position in bucket to insert entry */
        public byte* bucketOffsets;

        public _splitIndices_e__FixedBuffer splitIndices;

        public _matchCandidates_e__FixedBuffer matchCandidates;

        public unsafe partial struct _splitIndices_e__FixedBuffer
        {
            public nuint e0;
            public nuint e1;
            public nuint e2;
            public nuint e3;
            public nuint e4;
            public nuint e5;
            public nuint e6;
            public nuint e7;
            public nuint e8;
            public nuint e9;
            public nuint e10;
            public nuint e11;
            public nuint e12;
            public nuint e13;
            public nuint e14;
            public nuint e15;
            public nuint e16;
            public nuint e17;
            public nuint e18;
            public nuint e19;
            public nuint e20;
            public nuint e21;
            public nuint e22;
            public nuint e23;
            public nuint e24;
            public nuint e25;
            public nuint e26;
            public nuint e27;
            public nuint e28;
            public nuint e29;
            public nuint e30;
            public nuint e31;
            public nuint e32;
            public nuint e33;
            public nuint e34;
            public nuint e35;
            public nuint e36;
            public nuint e37;
            public nuint e38;
            public nuint e39;
            public nuint e40;
            public nuint e41;
            public nuint e42;
            public nuint e43;
            public nuint e44;
            public nuint e45;
            public nuint e46;
            public nuint e47;
            public nuint e48;
            public nuint e49;
            public nuint e50;
            public nuint e51;
            public nuint e52;
            public nuint e53;
            public nuint e54;
            public nuint e55;
            public nuint e56;
            public nuint e57;
            public nuint e58;
            public nuint e59;
            public nuint e60;
            public nuint e61;
            public nuint e62;
            public nuint e63;

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
            public static implicit operator nuint*(in _splitIndices_e__FixedBuffer t)
            {
                Ldarg_0();
                Ldflda(new FieldRef(typeof(_splitIndices_e__FixedBuffer), nameof(e0)));
                return IL.ReturnPointer<nuint>();
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            [InlineMethod.Inline]
            public static nuint* operator +(in _splitIndices_e__FixedBuffer t, uint index)
            {
                Ldarg_0();
                Ldflda(new FieldRef(typeof(_splitIndices_e__FixedBuffer), nameof(e0)));
                Ldarg_1();
                Conv_I();
                Sizeof<nuint>();
                Conv_I();
                Mul();
                Add();
                return IL.ReturnPointer<nuint>();
            }
        }

        public unsafe partial struct _matchCandidates_e__FixedBuffer
        {
            public ldmMatchCandidate_t e0;
            public ldmMatchCandidate_t e1;
            public ldmMatchCandidate_t e2;
            public ldmMatchCandidate_t e3;
            public ldmMatchCandidate_t e4;
            public ldmMatchCandidate_t e5;
            public ldmMatchCandidate_t e6;
            public ldmMatchCandidate_t e7;
            public ldmMatchCandidate_t e8;
            public ldmMatchCandidate_t e9;
            public ldmMatchCandidate_t e10;
            public ldmMatchCandidate_t e11;
            public ldmMatchCandidate_t e12;
            public ldmMatchCandidate_t e13;
            public ldmMatchCandidate_t e14;
            public ldmMatchCandidate_t e15;
            public ldmMatchCandidate_t e16;
            public ldmMatchCandidate_t e17;
            public ldmMatchCandidate_t e18;
            public ldmMatchCandidate_t e19;
            public ldmMatchCandidate_t e20;
            public ldmMatchCandidate_t e21;
            public ldmMatchCandidate_t e22;
            public ldmMatchCandidate_t e23;
            public ldmMatchCandidate_t e24;
            public ldmMatchCandidate_t e25;
            public ldmMatchCandidate_t e26;
            public ldmMatchCandidate_t e27;
            public ldmMatchCandidate_t e28;
            public ldmMatchCandidate_t e29;
            public ldmMatchCandidate_t e30;
            public ldmMatchCandidate_t e31;
            public ldmMatchCandidate_t e32;
            public ldmMatchCandidate_t e33;
            public ldmMatchCandidate_t e34;
            public ldmMatchCandidate_t e35;
            public ldmMatchCandidate_t e36;
            public ldmMatchCandidate_t e37;
            public ldmMatchCandidate_t e38;
            public ldmMatchCandidate_t e39;
            public ldmMatchCandidate_t e40;
            public ldmMatchCandidate_t e41;
            public ldmMatchCandidate_t e42;
            public ldmMatchCandidate_t e43;
            public ldmMatchCandidate_t e44;
            public ldmMatchCandidate_t e45;
            public ldmMatchCandidate_t e46;
            public ldmMatchCandidate_t e47;
            public ldmMatchCandidate_t e48;
            public ldmMatchCandidate_t e49;
            public ldmMatchCandidate_t e50;
            public ldmMatchCandidate_t e51;
            public ldmMatchCandidate_t e52;
            public ldmMatchCandidate_t e53;
            public ldmMatchCandidate_t e54;
            public ldmMatchCandidate_t e55;
            public ldmMatchCandidate_t e56;
            public ldmMatchCandidate_t e57;
            public ldmMatchCandidate_t e58;
            public ldmMatchCandidate_t e59;
            public ldmMatchCandidate_t e60;
            public ldmMatchCandidate_t e61;
            public ldmMatchCandidate_t e62;
            public ldmMatchCandidate_t e63;

            public ref ldmMatchCandidate_t this[int index]
            {
                [MethodImpl(MethodImplOptions.AggressiveInlining)]
                [InlineMethod.Inline]
                get => ref *(this + (uint)index);
            }

            public ref ldmMatchCandidate_t this[uint index]
            {
                [MethodImpl(MethodImplOptions.AggressiveInlining)]
                [InlineMethod.Inline]
                get => ref *(this + index);
            }

            public ref ldmMatchCandidate_t this[nuint index]
            {
                [MethodImpl(MethodImplOptions.AggressiveInlining)]
                [InlineMethod.Inline]
                get => ref *(this + (uint)index);
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            [InlineMethod.Inline]
            public static implicit operator ldmMatchCandidate_t*(in _matchCandidates_e__FixedBuffer t)
            {
                Ldarg_0();
                Ldflda(new FieldRef(typeof(_matchCandidates_e__FixedBuffer), nameof(e0)));
                return IL.ReturnPointer<ldmMatchCandidate_t>();
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            [InlineMethod.Inline]
            public static ldmMatchCandidate_t* operator +(in _matchCandidates_e__FixedBuffer t, uint index)
            {
                Ldarg_0();
                Ldflda(new FieldRef(typeof(_matchCandidates_e__FixedBuffer), nameof(e0)));
                Ldarg_1();
                Conv_I();
                Sizeof<ldmMatchCandidate_t>();
                Conv_I();
                Mul();
                Add();
                return IL.ReturnPointer<ldmMatchCandidate_t>();
            }
        }
    }
}
