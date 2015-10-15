namespace SharpCompress.Common
{
    using System;

    [Flags]
    public enum ExtractOptions
    {
        ExtractFullPath = 2,
        None = 0,
        Overwrite = 1,
        PreserveAttributes = 8,
        PreserveFileTime = 4
    }
}

