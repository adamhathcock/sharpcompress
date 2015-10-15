namespace SharpCompress.Common.Rar.Headers
{
    using System;

    [Flags]
    internal enum EndArchiveFlags
    {
        EARC_DATACRC = 2,
        EARC_NEXT_VOLUME = 1,
        EARC_REVSPACE = 4,
        EARC_VOLNUMBER = 8
    }
}

