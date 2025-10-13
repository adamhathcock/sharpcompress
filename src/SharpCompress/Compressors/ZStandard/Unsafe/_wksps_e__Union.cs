using System.Runtime.InteropServices;

namespace SharpCompress.Compressors.ZStandard.Unsafe;

[StructLayout(LayoutKind.Explicit)]
public unsafe struct _wksps_e__Union
{
    [FieldOffset(0)]
    public HUF_buildCTable_wksp_tables buildCTable_wksp;

    [FieldOffset(0)]
    public HUF_WriteCTableWksp writeCTable_wksp;

    [FieldOffset(0)]
    public fixed uint hist_wksp[1024];
}
