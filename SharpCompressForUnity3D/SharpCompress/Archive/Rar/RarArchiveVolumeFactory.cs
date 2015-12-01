namespace SharpCompress.Archive.Rar
{
    using SharpCompress.Common;
    using SharpCompress.Common.Rar;
    using SharpCompress.Common.Rar.Headers;
    using System;
    using System.Collections;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.IO;
    using System.Runtime.CompilerServices;
    using System.Threading;

    internal static class RarArchiveVolumeFactory
    {
        private static bool af_HasFlag(ArchiveFlags archiveFlags, ArchiveFlags archiveFlags2)
        {
            return ((archiveFlags & archiveFlags2) == archiveFlags2);
        }

        internal static IEnumerable<RarVolume> GetParts(IEnumerable<Stream> streams, string password, Options options)
        {
            foreach (Stream iteratorVariable0 in streams)
            {
                if (!(iteratorVariable0.CanRead && iteratorVariable0.CanSeek))
                {
                    throw new ArgumentException("Stream is not readable and seekable");
                }
                StreamRarArchiveVolume iteratorVariable1 = new StreamRarArchiveVolume(iteratorVariable0, password, options);
                yield return iteratorVariable1;
            }
        }

    }
}

