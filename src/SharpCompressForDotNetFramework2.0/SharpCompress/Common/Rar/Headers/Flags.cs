using System;

namespace SharpCompress.Common.Rar.Headers
{
    internal enum HeaderType
    {
        MarkHeader = 0x72,
        ArchiveHeader = 0x73,
        FileHeader = 0x74,
        CommHeader = 0x75,
        AvHeader = 0x76,
        SubHeader = 0x77,
        ProtectHeader = 0x78,
        SignHeader = 0x79,
        NewSubHeader = 0x7a,
        EndArchiveHeader = 0x7b,
    }

    internal enum HeaderFlags : short
    {
        LONG_BLOCK = -0x8000,
    }

    [Flags]
    internal enum ArchiveFlags
    {
        VOLUME = 0x0001,
        COMMENT = 0x0002,
        LOCK = 0x0004,
        SOLID = 0x0008,
        NEWNUMBERING = 0x0010,
        AV = 0x0020,
        PROTECT = 0x0040,
        PASSWORD = 0x0080,
        FIRSTVOLUME = 0x0100,
        ENCRYPTVER = 0x0200,
    }

    internal enum HostOS
    {
        MSDOS = 0,
        OS2 = 1,
        Win32 = 2,
        Unix = 3,
        MacOS = 4,
        BeOS = 5
    }

    [Flags]
    internal enum FileFlags : ushort
    {
        SPLIT_BEFORE = 0x0001,
        SPLIT_AFTER = 0x0002,
        PASSWORD = 0x0004,
        COMMENT = 0x0008,
        SOLID = 0x0010,

        WINDOWMASK = 0x00e0,
        WINDOW64 = 0x0000,
        WINDOW128 = 0x0020,
        WINDOW256 = 0x0040,
        WINDOW512 = 0x0060,
        WINDOW1024 = 0x0080,
        WINDOW2048 = 0x00a0,
        WINDOW4096 = 0x00c0,
        DIRECTORY = 0x00e0,

        LARGE = 0x0100,
        UNICODE = 0x0200,
        SALT = 0x0400,
        VERSION = 0x0800,
        EXTTIME = 0x1000,
        EXTFLAGS = 0x2000,
    }


    [Flags]
    internal enum EndArchiveFlags
    {
        EARC_NEXT_VOLUME = 0x0001,
        EARC_DATACRC = 0x0002,
        EARC_REVSPACE = 0x0004,
        EARC_VOLNUMBER = 0x0008,
    }
}
