using System;

namespace SharpCompress.Writers.SevenZip;

/// <summary>
/// Configures how consecutive file writes are grouped into shared 7z folders.
/// </summary>
public sealed record SevenZipSolidOptions
{
    /// <summary>
    /// Disables solid grouping so each file is compressed independently.
    /// </summary>
    public static SevenZipSolidOptions Disabled { get; } = new();

    /// <summary>
    /// Determines how consecutive files are grouped.
    /// </summary>
    public SevenZipSolidMode Mode { get; init; } = SevenZipSolidMode.None;

    /// <summary>
    /// Resolves a group key when <see cref="Mode" /> is <see cref="SevenZipSolidMode.Custom" />.
    /// Returning <see langword="null" /> writes the file as its own folder.
    /// </summary>
    public Func<SevenZipWriteContext, string?>? GroupKeySelector { get; init; }

    internal void Validate()
    {
        if (Mode == SevenZipSolidMode.Custom && GroupKeySelector is null)
        {
            throw new ArgumentException(
                "SevenZip solid grouping in Custom mode requires GroupKeySelector."
            );
        }
    }

    internal string? ResolveGroupKey(SevenZipWriteContext context) =>
        Mode switch
        {
            SevenZipSolidMode.None => null,
            SevenZipSolidMode.All => string.Empty,
            SevenZipSolidMode.ByDirectory => context.DirectoryPath,
            SevenZipSolidMode.ByExtension => context.Extension,
            SevenZipSolidMode.Custom => GroupKeySelector!(context),
            _ => throw new InvalidOperationException($"Unsupported solid mode: {Mode}"),
        };
}
