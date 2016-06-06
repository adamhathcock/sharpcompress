using System;

namespace SharpCompress.Common
{
    [Flags]
    public enum ExtractOptions
    {
        None = 0,

        /// <summary>
        /// overwrite target if it exists
        /// </summary>
        Overwrite = 1 << 0,

        /// <summary>
        /// extract with internal directory structure
        /// </summary>
        ExtractFullPath = 1 << 1,

        /// <summary>
        /// preserve file time
        /// </summary>
        PreserveFileTime = 1 << 2,

        /// <summary>
        /// preserve windows file attributes
        /// </summary>
        PreserveAttributes = 1 << 3,
    }
}