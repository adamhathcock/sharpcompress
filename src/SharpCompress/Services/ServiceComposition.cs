using Pure.DI;
using SharpCompress.Factories;

namespace SharpCompress.Services;

// Only stateless workflow services are composed. Returned archives and streams stay caller-owned.
internal partial class ServiceComposition
{
    private static void Setup() =>
        DI.Setup()
            .Hint(Hint.Resolve, "Off")
            .Arg<FactoryCatalog>("catalog")
            .Bind()
            .As(Lifetime.Singleton)
            .To<ReaderService>()
            .Bind()
            .As(Lifetime.Singleton)
            .To<ArchiveService>()
            .Bind()
            .As(Lifetime.Singleton)
            .To<WriterService>()
            .Bind()
            .As(Lifetime.Singleton)
            .To<ArchiveExtractor>()
            .Bind()
            .As(Lifetime.Singleton)
            .To<ArchiveFileWriter>()
            .Root<ArchiveService>("Archives")
            .Root<ReaderService>("Readers")
            .Root<WriterService>("Writers")
            .Root<ArchiveExtractor>("Extractor")
            .Root<ArchiveFileWriter>("FileWriter");
}

internal static class LibraryDefaults
{
    // Do not construct built-in factories here: Factory owns their initialization and registration.
    public static ServiceComposition Composition { get; } =
        new(new FactoryCatalog(() => Factory.Factories, () => TarWrapper.Wrappers));
}
