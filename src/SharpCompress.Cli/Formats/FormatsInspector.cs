using SharpCompress.Archives;
using SharpCompress.Factories;
using SharpCompress.Readers;

namespace SharpCompress.Cli.Formats;

public sealed class FormatsInspector
{
    public List<FormatSupportResult> GetFormats()
    {
        return Factory
            .Factories.OfType<IFactory>()
            .Select(factory =>
            {
                var extensions = factory
                    .GetSupportedExtensions()
                    .Distinct()
                    .OrderBy(extension => extension)
                    .ToList();

                return new FormatSupportResult(
                    Name: factory.Name,
                    ArchiveType: factory.KnownArchiveType?.ToString() ?? "Unknown",
                    Extensions: extensions,
                    SupportsForward: factory is IReaderFactory,
                    SupportsSeekable: factory is IArchiveFactory
                );
            })
            .OrderBy(result => result.Name)
            .ToList();
    }
}
