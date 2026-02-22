using SharpCompress.Archives;
using SharpCompress.Common;
using SharpCompress.Factories;
using SharpCompress.Readers;

namespace SharpCompress.Cli.Inspection;

internal static class ArchiveTypeCapabilities
{
    private static readonly Dictionary<ArchiveType, Capability> Capabilities = Factory
        .Factories.OfType<IFactory>()
        .Where(factory => factory.KnownArchiveType.HasValue)
        .GroupBy(factory => factory.KnownArchiveType!.Value)
        .ToDictionary(
            group => group.Key,
            group => new Capability(
                SupportsForward: group.Any(factory => factory is IReaderFactory),
                SupportsSeekable: group.Any(factory => factory is IArchiveFactory)
            )
        );

    public static Capability Get(ArchiveType archiveType)
    {
        if (Capabilities.TryGetValue(archiveType, out var capability))
        {
            return capability;
        }

        return new Capability(SupportsForward: true, SupportsSeekable: true);
    }

    public sealed record Capability(bool SupportsForward, bool SupportsSeekable);
}
