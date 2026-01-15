using System.Linq;
using System.Threading.Tasks;
using SharpCompress.Common.Rar;

namespace SharpCompress.Archives.Rar;

public static class RarArchiveExtensions
{
    extension(IRarArchive archive)
    {
        /// <summary>
        /// RarArchive is the first volume of a multi-part archive.  If MultipartVolume is true and IsFirstVolume is false then the first volume file must be missing.
        /// </summary>
        public bool IsFirstVolume() => archive.Volumes.Cast<RarVolume>().First().IsFirstVolume;

        /// <summary>
        /// RarArchive is part of a multi-part archive.
        /// </summary>
        public bool IsMultipartVolume() => archive.Volumes.Cast<RarVolume>().First().IsMultiVolume;
    }

    extension(IRarAsyncArchive archive)
    {
        /// <summary>
        /// RarArchive is the first volume of a multi-part archive.  If MultipartVolume is true and IsFirstVolume is false then the first volume file must be missing.
        /// </summary>
        public async ValueTask<bool> IsFirstVolumeAsync() =>
            (await archive.VolumesAsync.CastAsync<RarVolume>().FirstAsync()).IsFirstVolume;

        /// <summary>
        /// RarArchive is part of a multi-part archive.
        /// </summary>
        public async ValueTask<bool> IsMultipartVolumeAsync() =>
            (await archive.VolumesAsync.CastAsync<RarVolume>().FirstAsync()).IsMultiVolume;
    }
}
