namespace SharpCompress.Common.Rar.Headers
{
    using System;

    [Flags]
    internal enum ArchiveFlags
    {
        AV = 0x20,
        COMMENT = 2,
        ENCRYPTVER = 0x200,
        FIRSTVOLUME = 0x100,
        LOCK = 4,
        NEWNUMBERING = 0x10,
        PASSWORD = 0x80,
        PROTECT = 0x40,
        SOLID = 8,
        VOLUME = 1
    }
}

