using Pure.DI;
using SharpCompress.Factories;

namespace SharpCompress.Services;

// Only stateless workflow services are composed. Returned archives and streams stay caller-owned.
internal partial class ServiceComposition
{
    private static void Setup() =>
        DI.Setup()
            .Hint(Hint.Resolve, "Off")
            .Arg<IFactoryCatalog>("catalog")
            .Bind<IReaderService>()
            .As(Lifetime.Singleton)
            .To<ReaderService>()
            .Bind<IArchiveService>()
            .As(Lifetime.Singleton)
            .To<ArchiveService>()
            .Bind<IWriterService>()
            .As(Lifetime.Singleton)
            .To<WriterService>()
            .Bind<IArchiveExtractor>()
            .As(Lifetime.Singleton)
            .To<ArchiveExtractor>()
            .Bind<IArchiveFileWriter>()
            .As(Lifetime.Singleton)
            .To<ArchiveFileWriter>()
            .Root<IArchiveService>("Archives")
            .Root<IReaderService>("Readers")
            .Root<IWriterService>("Writers")
            .Root<IArchiveExtractor>("Extractor")
            .Root<IArchiveFileWriter>("FileWriter");
}

internal static class LibraryDefaults
{
    // Do not construct built-in factories here: Factory owns their initialization and registration.
    public static ServiceComposition Composition { get; } =
        new(new FactoryCatalog(() => Factory.Factories, () => TarWrapper.Wrappers));
}
