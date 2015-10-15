namespace SharpCompress.Common.Rar.Headers
{
    using System;

    [Flags]
    internal enum FileFlags : ushort
    {
        COMMENT = 8,
        DIRECTORY = 0xe0,
        EXTFLAGS = 0x2000,
        EXTTIME = 0x1000,
        LARGE = 0x100,
        PASSWORD = 4,
        SALT = 0x400,
        SOLID = 0x10,
        SPLIT_AFTER = 2,
        SPLIT_BEFORE = 1,
        UNICODE = 0x200,
        VERSION = 0x800,
        WINDOW1024 = 0x80,
        WINDOW128 = 0x20,
        WINDOW2048 = 160,
        WINDOW256 = 0x40,
        WINDOW4096 = 0xc0,
        WINDOW512 = 0x60,
        WINDOW64 = 0,
        WINDOWMASK = 0xe0
    }
}

