namespace SharpCompress.Common.SevenZip
{
    internal struct CMethodId
    {
        public const ulong kCopyId = 0;
        public const ulong kLzmaId = 0x030101;
        public const ulong kLzma2Id = 0x21;
        public const ulong kAESId = 0x06F10701;

        public static readonly CMethodId kCopy = new CMethodId(kCopyId);
        public static readonly CMethodId kLzma = new CMethodId(kLzmaId);
        public static readonly CMethodId kLzma2 = new CMethodId(kLzma2Id);
        public static readonly CMethodId kAES = new CMethodId(kAESId);

        public readonly ulong Id;

        public CMethodId(ulong id)
        {
            this.Id = id;
        }

        public override int GetHashCode()
        {
            return Id.GetHashCode();
        }

        public override bool Equals(object obj)
        {
            return obj is CMethodId && (CMethodId) obj == this;
        }

        public bool Equals(CMethodId other)
        {
            return Id == other.Id;
        }

        public static bool operator ==(CMethodId left, CMethodId right)
        {
            return left.Id == right.Id;
        }

        public static bool operator !=(CMethodId left, CMethodId right)
        {
            return left.Id != right.Id;
        }

        public int GetLength()
        {
            int bytes = 0;
            for (ulong value = Id; value != 0; value >>= 8)
                bytes++;
            return bytes;
        }
    }
}