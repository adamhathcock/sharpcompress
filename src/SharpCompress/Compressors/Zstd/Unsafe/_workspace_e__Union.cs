using System;
using System.Runtime.InteropServices;

namespace ZstdSharp.Unsafe
{
    [StructLayout(LayoutKind.Explicit)]
    public unsafe partial struct _workspace_e__Union
    {
        [FieldOffset(0)]
        public fixed uint hist_wksp[1024];

        [FieldOffset(0)]
        public fixed byte scratchBuffer[4096];
    }
}
