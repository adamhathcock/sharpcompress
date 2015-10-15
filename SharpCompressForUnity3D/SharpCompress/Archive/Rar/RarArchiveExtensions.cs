namespace SharpCompress.Archive.Rar
{
    using System;
    using System.Linq;
    using System.Runtime.CompilerServices;

    [Extension]
    public static class RarArchiveExtensions
    {
        [Extension]
        public static bool IsFirstVolume(RarArchive archive)
        {
            return Enumerable.First<RarVolume>(archive.Volumes).IsFirstVolume;
        }

        [Extension]
        public static bool IsMultipartVolume(RarArchive archive)
        {
            return Enumerable.First<RarVolume>(archive.Volumes).IsMultiVolume;
        }
    }
}

