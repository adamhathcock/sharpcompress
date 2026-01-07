namespace SharpCompress.Common.Ace.Headers
{
    /// <summary>
    /// Header flags (main + file, overlapping meanings)
    /// </summary>
    public static class HeaderFlags
    {
        // Shared / low bits
        public const ushort ADDSIZE = 0x0001; // extra size field present
        public const ushort COMMENT = 0x0002; // comment present
        public const ushort MEMORY_64BIT = 0x0004;
        public const ushort AV_STRING = 0x0008; // AV string present
        public const ushort SOLID = 0x0010; // solid file
        public const ushort LOCKED = 0x0020;
        public const ushort PROTECTED = 0x0040;

        // Main header specific
        public const ushort V20FORMAT = 0x0100;
        public const ushort SFX = 0x0200;
        public const ushort LIMITSFXJR = 0x0400;
        public const ushort MULTIVOLUME = 0x0800;
        public const ushort ADVERT = 0x1000;
        public const ushort RECOVERY = 0x2000;
        public const ushort LOCKED_MAIN = 0x4000;
        public const ushort SOLID_MAIN = 0x8000;

        // File header specific (same bits, different meaning)
        public const ushort NTSECURITY = 0x0400;
        public const ushort CONTINUED_PREV = 0x1000;
        public const ushort CONTINUED_NEXT = 0x2000;
        public const ushort FILE_ENCRYPTED = 0x4000; // file encrypted (file header)
    }
}
