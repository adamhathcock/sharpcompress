using System;
#if !PORTABLE
using System.IO;
#endif

namespace SharpCompress.Common
{
    public interface IVolume : IDisposable
    {
#if !PORTABLE
        /// <summary>
        /// File that backs this volume, if it not stream based
        /// </summary>
        FileInfo VolumeFile
        {
            get;
        }
#endif
    }
}
