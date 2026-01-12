using System.Linq;
using System.Threading.Tasks;
using SharpCompress.Common.Rar;

namespace SharpCompress.Archives.Rar;

public static class RarArchiveExtensions
{
    /// <summary>
    /// RarArchive is the first volume of a multi-part archive.  If MultipartVolume is true and IsFirstVolume is false then the first volume file must be missing.
    /// </summary>
    public static bool IsFirstVolume(this IRarArchive archive) =>
        archive.Volumes.Cast<RarVolume>().First().IsFirstVolume;

    /// <summary>
    /// RarArchive is part of a multi-part archive.
    /// </summary>
    public static bool IsMultipartVolume(this IRarArchive archive) =>
        archive.Volumes.Cast<RarVolume>().First().IsMultiVolume;


    /// <summary>
    /// RarArchive is the first volume of a multi-part archive.  If MultipartVolume is true and IsFirstVolume is false then the first volume file must be missing.
    /// </summary>
    public static async ValueTask<bool> IsFirstVolumeAsync(this IRarAsyncArchive archive) =>
        (await archive.VolumesAsync.CastAsync<RarVolume>().FirstAsync()).IsFirstVolume;

    /// <summary>
    /// RarArchive is part of a multi-part archive.
    /// </summary>
    public static async ValueTask<bool> IsMultipartVolumeAsync(this IRarAsyncArchive archive) =>
        (await archive.VolumesAsync.CastAsync<RarVolume>().FirstAsync()).IsMultiVolume;
}
