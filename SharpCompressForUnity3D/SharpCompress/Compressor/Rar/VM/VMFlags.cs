namespace SharpCompress.Compressor.Rar.VM
{
    using System;

    internal enum VMFlags
    {
        None = 0,
        VM_FC = 1,
        VM_FS = 0x4c4b400,
        VM_FZ = 2
    }
}

