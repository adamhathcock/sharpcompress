namespace SharpCompress.Common.Zip.Headers
{
    using System;

    internal enum ZipHeaderType
    {
        Ignore,
        LocalEntry,
        DirectoryEntry,
        DirectoryEnd,
        Split
    }
}

