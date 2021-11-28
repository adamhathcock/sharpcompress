using System;
using System.Runtime.InteropServices;

namespace ZstdSharp.Unsafe
{
    [StructLayout(LayoutKind.Explicit)]
    public partial struct _wksps_e__Union
    {
        [FieldOffset(0)]
        public HUF_buildCTable_wksp_tables buildCTable_wksp;

        [FieldOffset(0)]
        public HUF_WriteCTableWksp writeCTable_wksp;
    }
}
