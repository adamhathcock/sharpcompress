using System;

namespace SharpCompress.Common
{
    [Flags]
    public enum ArchiveTypeMask
    {
        None = 0,
        Rar = 1,
        Zip = 1 << 1,
        Tar = 1 << 2,
        SevenZip = 1 << 3,
        GZip = 1 << 4,
        BZip2 = 1 << 5,
        LZip = 1 << 6,

        AllExceptTar = Rar | Zip | SevenZip | GZip | BZip2 | LZip,
        All = AllExceptTar | Tar
    }
}
