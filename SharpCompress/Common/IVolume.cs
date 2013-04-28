using System;
#if !PORTABLE && !NETFX_CORE
using System.IO;
#endif

namespace SharpCompress.Common
{
    public interface IVolume : IDisposable
    {
#if !PORTABLE && !NETFX_CORE
        /// <summary>
        /// File that backs this volume, if it not stream based
        /// </summary>
        FileInfo VolumeFile { get; }
#endif
    }
}