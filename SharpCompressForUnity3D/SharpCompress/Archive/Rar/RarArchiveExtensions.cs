namespace SharpCompress.Archive.Rar
{
    using System;
    using System.Linq;
    using System.Runtime.CompilerServices;

    ////[Extension]
    //public static class RarArchiveExtensions
    //{
    //    //[Extension]
    //    public static bool IsFirstVolume(this RarArchive archive)
    //    {
    //        return Enumerable.First<RarVolume>(archive.Volumes).IsFirstVolume;
    //    }

    //    //[Extension]
    //    public static bool IsMultipartVolume(this RarArchive archive)
    //    {
    //        return Enumerable.First<RarVolume>(archive.Volumes).IsMultiVolume;
    //    }
    //}
    public static class RarArchiveExtensions {
        /// <summary>
        /// RarArchive is the first volume of a multi-part archive.  If MultipartVolume is true and IsFirstVolume is false then the first volume file must be missing.
        /// </summary>
        public static bool IsFirstVolume( RarArchive archive) {
            return archive.Volumes.First().IsFirstVolume;
        }

        /// <summary>
        /// RarArchive is part of a multi-part archive.
        /// </summary>
        public static bool IsMultipartVolume( RarArchive archive) {
            return archive.Volumes.First().IsMultiVolume;
        }
    }
}

