namespace SharpCompress.Common.SevenZip
{
    using System;
    using System.Runtime.InteropServices;

    [StructLayout(LayoutKind.Sequential)]
    internal struct CMethodId
    {
        public const ulong kCopyId = 0L;
        public const ulong kLzmaId = 0x30101L;
        public const ulong kLzma2Id = 0x21L;
        public const ulong kAESId = 0x6f10701L;
        public static readonly CMethodId kCopy;
        public static readonly CMethodId kLzma;
        public static readonly CMethodId kLzma2;
        public static readonly CMethodId kAES;
        public readonly ulong Id;
        public CMethodId(ulong id)
        {
            this.Id = id;
        }

        public override int GetHashCode()
        {
            return this.Id.GetHashCode();
        }

        public override bool Equals(object obj)
        {
            return ((obj is CMethodId) && (((CMethodId) obj) == this));
        }

        public bool Equals(CMethodId other)
        {
            return (this.Id == other.Id);
        }

        public static bool operator ==(CMethodId left, CMethodId right)
        {
            return (left.Id == right.Id);
        }

        public static bool operator !=(CMethodId left, CMethodId right)
        {
            return (left.Id != right.Id);
        }

        public int GetLength()
        {
            int num = 0;
            for (ulong i = this.Id; i != 0L; i = i >> 8)
            {
                num++;
            }
            return num;
        }

        static CMethodId()
        {
            kCopy = new CMethodId(0L);
            kLzma = new CMethodId(0x30101L);
            kLzma2 = new CMethodId(0x21L);
            kAES = new CMethodId(0x6f10701L);
        }
    }
}

