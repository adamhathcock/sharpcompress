namespace SharpCompress.Compressor.Rar.VM
{
    using System;

    internal class VMCmdFlags
    {
        public static byte[] VM_CmdFlags = new byte[] { 
            6, 70, 70, 70, 0x29, 0x29, 0x45, 0x45, 9, 70, 70, 70, 70, 0x29, 0x29, 0x29, 
            0x29, 0x29, 0x29, 1, 1, 0x11, 0x10, 5, 70, 70, 70, 0x45, 0, 0, 0x20, 0x40, 
            2, 2, 6, 6, 6, 0x66, 0x66, 0
         };
        public const byte VMCF_BYTEMODE = 4;
        public const byte VMCF_CHFLAGS = 0x40;
        public const byte VMCF_JUMP = 8;
        public const byte VMCF_OP0 = 0;
        public const byte VMCF_OP1 = 1;
        public const byte VMCF_OP2 = 2;
        public const byte VMCF_OPMASK = 3;
        public const byte VMCF_PROC = 0x10;
        public const byte VMCF_USEFLAGS = 0x20;
    }
}

