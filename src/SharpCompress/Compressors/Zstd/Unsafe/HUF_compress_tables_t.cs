using InlineIL;
using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using static InlineIL.IL.Emit;

namespace ZstdSharp.Unsafe
{
    public unsafe partial struct HUF_compress_tables_t
    {
        public fixed uint count[256];

        public _CTable_e__FixedBuffer CTable;

        public _wksps_e__Union wksps;

        public unsafe partial struct _CTable_e__FixedBuffer
        {
            public HUF_CElt_s e0;
            public HUF_CElt_s e1;
            public HUF_CElt_s e2;
            public HUF_CElt_s e3;
            public HUF_CElt_s e4;
            public HUF_CElt_s e5;
            public HUF_CElt_s e6;
            public HUF_CElt_s e7;
            public HUF_CElt_s e8;
            public HUF_CElt_s e9;
            public HUF_CElt_s e10;
            public HUF_CElt_s e11;
            public HUF_CElt_s e12;
            public HUF_CElt_s e13;
            public HUF_CElt_s e14;
            public HUF_CElt_s e15;
            public HUF_CElt_s e16;
            public HUF_CElt_s e17;
            public HUF_CElt_s e18;
            public HUF_CElt_s e19;
            public HUF_CElt_s e20;
            public HUF_CElt_s e21;
            public HUF_CElt_s e22;
            public HUF_CElt_s e23;
            public HUF_CElt_s e24;
            public HUF_CElt_s e25;
            public HUF_CElt_s e26;
            public HUF_CElt_s e27;
            public HUF_CElt_s e28;
            public HUF_CElt_s e29;
            public HUF_CElt_s e30;
            public HUF_CElt_s e31;
            public HUF_CElt_s e32;
            public HUF_CElt_s e33;
            public HUF_CElt_s e34;
            public HUF_CElt_s e35;
            public HUF_CElt_s e36;
            public HUF_CElt_s e37;
            public HUF_CElt_s e38;
            public HUF_CElt_s e39;
            public HUF_CElt_s e40;
            public HUF_CElt_s e41;
            public HUF_CElt_s e42;
            public HUF_CElt_s e43;
            public HUF_CElt_s e44;
            public HUF_CElt_s e45;
            public HUF_CElt_s e46;
            public HUF_CElt_s e47;
            public HUF_CElt_s e48;
            public HUF_CElt_s e49;
            public HUF_CElt_s e50;
            public HUF_CElt_s e51;
            public HUF_CElt_s e52;
            public HUF_CElt_s e53;
            public HUF_CElt_s e54;
            public HUF_CElt_s e55;
            public HUF_CElt_s e56;
            public HUF_CElt_s e57;
            public HUF_CElt_s e58;
            public HUF_CElt_s e59;
            public HUF_CElt_s e60;
            public HUF_CElt_s e61;
            public HUF_CElt_s e62;
            public HUF_CElt_s e63;
            public HUF_CElt_s e64;
            public HUF_CElt_s e65;
            public HUF_CElt_s e66;
            public HUF_CElt_s e67;
            public HUF_CElt_s e68;
            public HUF_CElt_s e69;
            public HUF_CElt_s e70;
            public HUF_CElt_s e71;
            public HUF_CElt_s e72;
            public HUF_CElt_s e73;
            public HUF_CElt_s e74;
            public HUF_CElt_s e75;
            public HUF_CElt_s e76;
            public HUF_CElt_s e77;
            public HUF_CElt_s e78;
            public HUF_CElt_s e79;
            public HUF_CElt_s e80;
            public HUF_CElt_s e81;
            public HUF_CElt_s e82;
            public HUF_CElt_s e83;
            public HUF_CElt_s e84;
            public HUF_CElt_s e85;
            public HUF_CElt_s e86;
            public HUF_CElt_s e87;
            public HUF_CElt_s e88;
            public HUF_CElt_s e89;
            public HUF_CElt_s e90;
            public HUF_CElt_s e91;
            public HUF_CElt_s e92;
            public HUF_CElt_s e93;
            public HUF_CElt_s e94;
            public HUF_CElt_s e95;
            public HUF_CElt_s e96;
            public HUF_CElt_s e97;
            public HUF_CElt_s e98;
            public HUF_CElt_s e99;
            public HUF_CElt_s e100;
            public HUF_CElt_s e101;
            public HUF_CElt_s e102;
            public HUF_CElt_s e103;
            public HUF_CElt_s e104;
            public HUF_CElt_s e105;
            public HUF_CElt_s e106;
            public HUF_CElt_s e107;
            public HUF_CElt_s e108;
            public HUF_CElt_s e109;
            public HUF_CElt_s e110;
            public HUF_CElt_s e111;
            public HUF_CElt_s e112;
            public HUF_CElt_s e113;
            public HUF_CElt_s e114;
            public HUF_CElt_s e115;
            public HUF_CElt_s e116;
            public HUF_CElt_s e117;
            public HUF_CElt_s e118;
            public HUF_CElt_s e119;
            public HUF_CElt_s e120;
            public HUF_CElt_s e121;
            public HUF_CElt_s e122;
            public HUF_CElt_s e123;
            public HUF_CElt_s e124;
            public HUF_CElt_s e125;
            public HUF_CElt_s e126;
            public HUF_CElt_s e127;
            public HUF_CElt_s e128;
            public HUF_CElt_s e129;
            public HUF_CElt_s e130;
            public HUF_CElt_s e131;
            public HUF_CElt_s e132;
            public HUF_CElt_s e133;
            public HUF_CElt_s e134;
            public HUF_CElt_s e135;
            public HUF_CElt_s e136;
            public HUF_CElt_s e137;
            public HUF_CElt_s e138;
            public HUF_CElt_s e139;
            public HUF_CElt_s e140;
            public HUF_CElt_s e141;
            public HUF_CElt_s e142;
            public HUF_CElt_s e143;
            public HUF_CElt_s e144;
            public HUF_CElt_s e145;
            public HUF_CElt_s e146;
            public HUF_CElt_s e147;
            public HUF_CElt_s e148;
            public HUF_CElt_s e149;
            public HUF_CElt_s e150;
            public HUF_CElt_s e151;
            public HUF_CElt_s e152;
            public HUF_CElt_s e153;
            public HUF_CElt_s e154;
            public HUF_CElt_s e155;
            public HUF_CElt_s e156;
            public HUF_CElt_s e157;
            public HUF_CElt_s e158;
            public HUF_CElt_s e159;
            public HUF_CElt_s e160;
            public HUF_CElt_s e161;
            public HUF_CElt_s e162;
            public HUF_CElt_s e163;
            public HUF_CElt_s e164;
            public HUF_CElt_s e165;
            public HUF_CElt_s e166;
            public HUF_CElt_s e167;
            public HUF_CElt_s e168;
            public HUF_CElt_s e169;
            public HUF_CElt_s e170;
            public HUF_CElt_s e171;
            public HUF_CElt_s e172;
            public HUF_CElt_s e173;
            public HUF_CElt_s e174;
            public HUF_CElt_s e175;
            public HUF_CElt_s e176;
            public HUF_CElt_s e177;
            public HUF_CElt_s e178;
            public HUF_CElt_s e179;
            public HUF_CElt_s e180;
            public HUF_CElt_s e181;
            public HUF_CElt_s e182;
            public HUF_CElt_s e183;
            public HUF_CElt_s e184;
            public HUF_CElt_s e185;
            public HUF_CElt_s e186;
            public HUF_CElt_s e187;
            public HUF_CElt_s e188;
            public HUF_CElt_s e189;
            public HUF_CElt_s e190;
            public HUF_CElt_s e191;
            public HUF_CElt_s e192;
            public HUF_CElt_s e193;
            public HUF_CElt_s e194;
            public HUF_CElt_s e195;
            public HUF_CElt_s e196;
            public HUF_CElt_s e197;
            public HUF_CElt_s e198;
            public HUF_CElt_s e199;
            public HUF_CElt_s e200;
            public HUF_CElt_s e201;
            public HUF_CElt_s e202;
            public HUF_CElt_s e203;
            public HUF_CElt_s e204;
            public HUF_CElt_s e205;
            public HUF_CElt_s e206;
            public HUF_CElt_s e207;
            public HUF_CElt_s e208;
            public HUF_CElt_s e209;
            public HUF_CElt_s e210;
            public HUF_CElt_s e211;
            public HUF_CElt_s e212;
            public HUF_CElt_s e213;
            public HUF_CElt_s e214;
            public HUF_CElt_s e215;
            public HUF_CElt_s e216;
            public HUF_CElt_s e217;
            public HUF_CElt_s e218;
            public HUF_CElt_s e219;
            public HUF_CElt_s e220;
            public HUF_CElt_s e221;
            public HUF_CElt_s e222;
            public HUF_CElt_s e223;
            public HUF_CElt_s e224;
            public HUF_CElt_s e225;
            public HUF_CElt_s e226;
            public HUF_CElt_s e227;
            public HUF_CElt_s e228;
            public HUF_CElt_s e229;
            public HUF_CElt_s e230;
            public HUF_CElt_s e231;
            public HUF_CElt_s e232;
            public HUF_CElt_s e233;
            public HUF_CElt_s e234;
            public HUF_CElt_s e235;
            public HUF_CElt_s e236;
            public HUF_CElt_s e237;
            public HUF_CElt_s e238;
            public HUF_CElt_s e239;
            public HUF_CElt_s e240;
            public HUF_CElt_s e241;
            public HUF_CElt_s e242;
            public HUF_CElt_s e243;
            public HUF_CElt_s e244;
            public HUF_CElt_s e245;
            public HUF_CElt_s e246;
            public HUF_CElt_s e247;
            public HUF_CElt_s e248;
            public HUF_CElt_s e249;
            public HUF_CElt_s e250;
            public HUF_CElt_s e251;
            public HUF_CElt_s e252;
            public HUF_CElt_s e253;
            public HUF_CElt_s e254;
            public HUF_CElt_s e255;

            public ref HUF_CElt_s this[int index]
            {
                [MethodImpl(MethodImplOptions.AggressiveInlining)]
                [InlineMethod.Inline]
                get => ref *(this + (uint)index);
            }

            public ref HUF_CElt_s this[uint index]
            {
                [MethodImpl(MethodImplOptions.AggressiveInlining)]
                [InlineMethod.Inline]
                get => ref *(this + index);
            }

            public ref HUF_CElt_s this[nuint index]
            {
                [MethodImpl(MethodImplOptions.AggressiveInlining)]
                [InlineMethod.Inline]
                get => ref *(this + (uint)index);
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            [InlineMethod.Inline]
            public static implicit operator HUF_CElt_s*(in _CTable_e__FixedBuffer t)
            {
                Ldarg_0();
                Ldflda(new FieldRef(typeof(_CTable_e__FixedBuffer), nameof(e0)));
                return IL.ReturnPointer<HUF_CElt_s>();
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            [InlineMethod.Inline]
            public static HUF_CElt_s* operator +(in _CTable_e__FixedBuffer t, uint index)
            {
                Ldarg_0();
                Ldflda(new FieldRef(typeof(_CTable_e__FixedBuffer), nameof(e0)));
                Ldarg_1();
                Conv_I();
                Sizeof<HUF_CElt_s>();
                Conv_I();
                Mul();
                Add();
                return IL.ReturnPointer<HUF_CElt_s>();
            }
        }
    }
}
