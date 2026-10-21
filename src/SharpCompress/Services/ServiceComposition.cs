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
            .Bind<IArchiveDetector>()
            .As(Lifetime.Singleton)
            .To<ArchiveDetector>()
            .Bind<IArchiveInspector>()
            .As(Lifetime.Singleton)
            .To<ArchiveInspector>()
            .Bind<IReaderExtractor>()
            .As(Lifetime.Singleton)
            .To<ReaderExtractor>()
            .Bind<IArchiveEntryExtractor>()
            .As(Lifetime.Singleton)
            .To<ArchiveEntryExtractor>()
            .Bind<IWriterService>()
            .As(Lifetime.Singleton)
            .To<WriterService>()
            .Bind<IArchiveExtractor>()
            .As(Lifetime.Singleton)
            .To<ArchiveExtractor>()
            .Bind<IWriterFileOperations>()
            .As(Lifetime.Singleton)
            .To<WriterFileOperations>()
            .Bind<IWritableArchiveFileOperations>()
            .As(Lifetime.Singleton)
            .To<WritableArchiveFileOperations>()
            .Root<IArchiveService>("Archives")
            .Root<IReaderService>("Readers")
            .Root<IWriterService>("Writers")
            .Root<IArchiveExtractor>("Extractor")
            .Root<IArchiveDetector>("Detector")
            .Root<IArchiveInspector>("Inspector")
            .Root<IReaderExtractor>("ReaderExtractor")
            .Root<IArchiveEntryExtractor>("EntryExtractor")
            .Root<IWriterFileOperations>("WriterFiles")
            .Root<IWritableArchiveFileOperations>("ArchiveFiles");
}

internal static class LibraryDefaults
{
    // Do not construct built-in factories here: Factory owns their initialization and registration.
    private static ServiceComposition Composition { get; } =
        new(new FactoryCatalog(() => Factory.Factories, () => TarWrapper.Wrappers));

    public static IArchiveService Archives => Composition.Archives;
    public static IReaderService Readers => Composition.Readers;
    public static IWriterService Writers => Composition.Writers;
    public static IArchiveExtractor Extractor => Composition.Extractor;
    public static IArchiveDetector Detector => Composition.Detector;
    public static IArchiveInspector Inspector => Composition.Inspector;
    public static IReaderExtractor ReaderExtractor => Composition.ReaderExtractor;
    public static IArchiveEntryExtractor EntryExtractor => Composition.EntryExtractor;
    public static IWriterFileOperations WriterFiles => Composition.WriterFiles;
    public static IWritableArchiveFileOperations ArchiveFiles => Composition.ArchiveFiles;
}
