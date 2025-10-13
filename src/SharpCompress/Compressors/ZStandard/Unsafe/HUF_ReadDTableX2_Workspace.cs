using System.Runtime.CompilerServices;

namespace ZstdSharp.Unsafe
{
    public unsafe struct HUF_ReadDTableX2_Workspace
    {
        public _rankVal_e__FixedBuffer rankVal;
        public fixed uint rankStats[13];
        public fixed uint rankStart0[15];
        public _sortedSymbol_e__FixedBuffer sortedSymbol;
        public fixed byte weightList[256];
        public fixed uint calleeWksp[219];
#if NET8_0_OR_GREATER
        [InlineArray(12)]
        public unsafe struct _rankVal_e__FixedBuffer
        {
            public rankValCol_t e0;
        }

#else
        public unsafe struct _rankVal_e__FixedBuffer
        {
            public rankValCol_t e0;
            public rankValCol_t e1;
            public rankValCol_t e2;
            public rankValCol_t e3;
            public rankValCol_t e4;
            public rankValCol_t e5;
            public rankValCol_t e6;
            public rankValCol_t e7;
            public rankValCol_t e8;
            public rankValCol_t e9;
            public rankValCol_t e10;
            public rankValCol_t e11;
        }
#endif

#if NET8_0_OR_GREATER
        [InlineArray(256)]
        public unsafe struct _sortedSymbol_e__FixedBuffer
        {
            public sortedSymbol_t e0;
        }

#else
        public unsafe struct _sortedSymbol_e__FixedBuffer
        {
            public sortedSymbol_t e0;
            public sortedSymbol_t e1;
            public sortedSymbol_t e2;
            public sortedSymbol_t e3;
            public sortedSymbol_t e4;
            public sortedSymbol_t e5;
            public sortedSymbol_t e6;
            public sortedSymbol_t e7;
            public sortedSymbol_t e8;
            public sortedSymbol_t e9;
            public sortedSymbol_t e10;
            public sortedSymbol_t e11;
            public sortedSymbol_t e12;
            public sortedSymbol_t e13;
            public sortedSymbol_t e14;
            public sortedSymbol_t e15;
            public sortedSymbol_t e16;
            public sortedSymbol_t e17;
            public sortedSymbol_t e18;
            public sortedSymbol_t e19;
            public sortedSymbol_t e20;
            public sortedSymbol_t e21;
            public sortedSymbol_t e22;
            public sortedSymbol_t e23;
            public sortedSymbol_t e24;
            public sortedSymbol_t e25;
            public sortedSymbol_t e26;
            public sortedSymbol_t e27;
            public sortedSymbol_t e28;
            public sortedSymbol_t e29;
            public sortedSymbol_t e30;
            public sortedSymbol_t e31;
            public sortedSymbol_t e32;
            public sortedSymbol_t e33;
            public sortedSymbol_t e34;
            public sortedSymbol_t e35;
            public sortedSymbol_t e36;
            public sortedSymbol_t e37;
            public sortedSymbol_t e38;
            public sortedSymbol_t e39;
            public sortedSymbol_t e40;
            public sortedSymbol_t e41;
            public sortedSymbol_t e42;
            public sortedSymbol_t e43;
            public sortedSymbol_t e44;
            public sortedSymbol_t e45;
            public sortedSymbol_t e46;
            public sortedSymbol_t e47;
            public sortedSymbol_t e48;
            public sortedSymbol_t e49;
            public sortedSymbol_t e50;
            public sortedSymbol_t e51;
            public sortedSymbol_t e52;
            public sortedSymbol_t e53;
            public sortedSymbol_t e54;
            public sortedSymbol_t e55;
            public sortedSymbol_t e56;
            public sortedSymbol_t e57;
            public sortedSymbol_t e58;
            public sortedSymbol_t e59;
            public sortedSymbol_t e60;
            public sortedSymbol_t e61;
            public sortedSymbol_t e62;
            public sortedSymbol_t e63;
            public sortedSymbol_t e64;
            public sortedSymbol_t e65;
            public sortedSymbol_t e66;
            public sortedSymbol_t e67;
            public sortedSymbol_t e68;
            public sortedSymbol_t e69;
            public sortedSymbol_t e70;
            public sortedSymbol_t e71;
            public sortedSymbol_t e72;
            public sortedSymbol_t e73;
            public sortedSymbol_t e74;
            public sortedSymbol_t e75;
            public sortedSymbol_t e76;
            public sortedSymbol_t e77;
            public sortedSymbol_t e78;
            public sortedSymbol_t e79;
            public sortedSymbol_t e80;
            public sortedSymbol_t e81;
            public sortedSymbol_t e82;
            public sortedSymbol_t e83;
            public sortedSymbol_t e84;
            public sortedSymbol_t e85;
            public sortedSymbol_t e86;
            public sortedSymbol_t e87;
            public sortedSymbol_t e88;
            public sortedSymbol_t e89;
            public sortedSymbol_t e90;
            public sortedSymbol_t e91;
            public sortedSymbol_t e92;
            public sortedSymbol_t e93;
            public sortedSymbol_t e94;
            public sortedSymbol_t e95;
            public sortedSymbol_t e96;
            public sortedSymbol_t e97;
            public sortedSymbol_t e98;
            public sortedSymbol_t e99;
            public sortedSymbol_t e100;
            public sortedSymbol_t e101;
            public sortedSymbol_t e102;
            public sortedSymbol_t e103;
            public sortedSymbol_t e104;
            public sortedSymbol_t e105;
            public sortedSymbol_t e106;
            public sortedSymbol_t e107;
            public sortedSymbol_t e108;
            public sortedSymbol_t e109;
            public sortedSymbol_t e110;
            public sortedSymbol_t e111;
            public sortedSymbol_t e112;
            public sortedSymbol_t e113;
            public sortedSymbol_t e114;
            public sortedSymbol_t e115;
            public sortedSymbol_t e116;
            public sortedSymbol_t e117;
            public sortedSymbol_t e118;
            public sortedSymbol_t e119;
            public sortedSymbol_t e120;
            public sortedSymbol_t e121;
            public sortedSymbol_t e122;
            public sortedSymbol_t e123;
            public sortedSymbol_t e124;
            public sortedSymbol_t e125;
            public sortedSymbol_t e126;
            public sortedSymbol_t e127;
            public sortedSymbol_t e128;
            public sortedSymbol_t e129;
            public sortedSymbol_t e130;
            public sortedSymbol_t e131;
            public sortedSymbol_t e132;
            public sortedSymbol_t e133;
            public sortedSymbol_t e134;
            public sortedSymbol_t e135;
            public sortedSymbol_t e136;
            public sortedSymbol_t e137;
            public sortedSymbol_t e138;
            public sortedSymbol_t e139;
            public sortedSymbol_t e140;
            public sortedSymbol_t e141;
            public sortedSymbol_t e142;
            public sortedSymbol_t e143;
            public sortedSymbol_t e144;
            public sortedSymbol_t e145;
            public sortedSymbol_t e146;
            public sortedSymbol_t e147;
            public sortedSymbol_t e148;
            public sortedSymbol_t e149;
            public sortedSymbol_t e150;
            public sortedSymbol_t e151;
            public sortedSymbol_t e152;
            public sortedSymbol_t e153;
            public sortedSymbol_t e154;
            public sortedSymbol_t e155;
            public sortedSymbol_t e156;
            public sortedSymbol_t e157;
            public sortedSymbol_t e158;
            public sortedSymbol_t e159;
            public sortedSymbol_t e160;
            public sortedSymbol_t e161;
            public sortedSymbol_t e162;
            public sortedSymbol_t e163;
            public sortedSymbol_t e164;
            public sortedSymbol_t e165;
            public sortedSymbol_t e166;
            public sortedSymbol_t e167;
            public sortedSymbol_t e168;
            public sortedSymbol_t e169;
            public sortedSymbol_t e170;
            public sortedSymbol_t e171;
            public sortedSymbol_t e172;
            public sortedSymbol_t e173;
            public sortedSymbol_t e174;
            public sortedSymbol_t e175;
            public sortedSymbol_t e176;
            public sortedSymbol_t e177;
            public sortedSymbol_t e178;
            public sortedSymbol_t e179;
            public sortedSymbol_t e180;
            public sortedSymbol_t e181;
            public sortedSymbol_t e182;
            public sortedSymbol_t e183;
            public sortedSymbol_t e184;
            public sortedSymbol_t e185;
            public sortedSymbol_t e186;
            public sortedSymbol_t e187;
            public sortedSymbol_t e188;
            public sortedSymbol_t e189;
            public sortedSymbol_t e190;
            public sortedSymbol_t e191;
            public sortedSymbol_t e192;
            public sortedSymbol_t e193;
            public sortedSymbol_t e194;
            public sortedSymbol_t e195;
            public sortedSymbol_t e196;
            public sortedSymbol_t e197;
            public sortedSymbol_t e198;
            public sortedSymbol_t e199;
            public sortedSymbol_t e200;
            public sortedSymbol_t e201;
            public sortedSymbol_t e202;
            public sortedSymbol_t e203;
            public sortedSymbol_t e204;
            public sortedSymbol_t e205;
            public sortedSymbol_t e206;
            public sortedSymbol_t e207;
            public sortedSymbol_t e208;
            public sortedSymbol_t e209;
            public sortedSymbol_t e210;
            public sortedSymbol_t e211;
            public sortedSymbol_t e212;
            public sortedSymbol_t e213;
            public sortedSymbol_t e214;
            public sortedSymbol_t e215;
            public sortedSymbol_t e216;
            public sortedSymbol_t e217;
            public sortedSymbol_t e218;
            public sortedSymbol_t e219;
            public sortedSymbol_t e220;
            public sortedSymbol_t e221;
            public sortedSymbol_t e222;
            public sortedSymbol_t e223;
            public sortedSymbol_t e224;
            public sortedSymbol_t e225;
            public sortedSymbol_t e226;
            public sortedSymbol_t e227;
            public sortedSymbol_t e228;
            public sortedSymbol_t e229;
            public sortedSymbol_t e230;
            public sortedSymbol_t e231;
            public sortedSymbol_t e232;
            public sortedSymbol_t e233;
            public sortedSymbol_t e234;
            public sortedSymbol_t e235;
            public sortedSymbol_t e236;
            public sortedSymbol_t e237;
            public sortedSymbol_t e238;
            public sortedSymbol_t e239;
            public sortedSymbol_t e240;
            public sortedSymbol_t e241;
            public sortedSymbol_t e242;
            public sortedSymbol_t e243;
            public sortedSymbol_t e244;
            public sortedSymbol_t e245;
            public sortedSymbol_t e246;
            public sortedSymbol_t e247;
            public sortedSymbol_t e248;
            public sortedSymbol_t e249;
            public sortedSymbol_t e250;
            public sortedSymbol_t e251;
            public sortedSymbol_t e252;
            public sortedSymbol_t e253;
            public sortedSymbol_t e254;
            public sortedSymbol_t e255;
        }
#endif
    }
}