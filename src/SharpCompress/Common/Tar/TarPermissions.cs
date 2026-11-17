using System;

namespace SharpCompress.Common.Tar;

[Flags]
internal enum TarPermissions : long
{
    None = 0,
    OtherExecute = 1,
    OtherWrite = 2,
    OtherRead = 4,
    GroupExecute = 8,
    GroupWrite = 16,
    GroupRead = 32,
    UserExecute = 64,
    UserWrite = 128,
    UserRead = 256,
    Sticky = 512,
    SetGroupId = 1024,
    SetUserId = 2048,

    ReadWriteExecute =
        UserRead
        | UserWrite
        | UserExecute
        | GroupRead
        | GroupWrite
        | GroupExecute
        | OtherRead
        | OtherWrite
        | OtherExecute,
    All = ReadWriteExecute | Sticky | SetGroupId | SetUserId,
}
