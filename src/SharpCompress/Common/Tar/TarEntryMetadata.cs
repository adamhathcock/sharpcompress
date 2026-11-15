namespace SharpCompress.Common.Tar;

internal readonly struct TarEntryMetadata
{
    internal const long DefaultUserId = 0;
    internal const long DefaultGroupId = 0;

    // Null means unspecified; zero is a real permission mode or numeric owner ID.
    internal long? Mode { get; init; }
    internal long? UserId { get; init; }
    internal long? GroupId { get; init; }

    internal long GetModeForWriting()
    {
        var permissions = (TarPermissions)(Mode ?? (long)TarPermissions.ReadWriteExecute);
        // Some headers include file-type bits. Only permission bits belong in the
        // written mode field; file/directory type is encoded separately.
        return (long)(permissions & TarPermissions.All);
    }

    internal long GetLegacyMode(bool isDirectory)
    {
        var permissions = (TarPermissions)(Mode ?? 0);
        // The public API historically marks every directory with 01000, which
        // overlaps the sticky bit. Never add that marker to the stored metadata.
        if (isDirectory)
        {
            permissions |= TarPermissions.Sticky;
        }
        return (long)permissions;
    }
}
