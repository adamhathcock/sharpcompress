using Pure.DI;
using SharpCompress.Archives;
using SharpCompress.Readers;
using SharpCompress.Writers;

namespace SharpCompress;

internal sealed class ClientServices(
    ArchiveService archives,
    ReaderService readers,
    WriterService writers,
    IArchiveExtractor extractor,
    IArchiveFileWriter fileWriter
)
{
    public ArchiveService Archives { get; } = archives;
    public ReaderService Readers { get; } = readers;
    public WriterService Writers { get; } = writers;
    public IArchiveExtractor Extractor { get; } = extractor;
    public IArchiveFileWriter FileWriter { get; } = fileWriter;
}

// The generator is a build-time implementation detail; none of its types enter the public API.
internal partial class ServiceComposition
{
    private static void Setup() =>
        DI.Setup()
            .Hint(Hint.Resolve, "Off")
            .Arg<SharpCompressConfiguration>("configuration")
            .Bind()
            .To<ArchiveService>()
            .Bind()
            .To<ReaderService>()
            .Bind()
            .To<WriterService>()
            .Bind<IArchiveExtractor>()
            .To<ArchiveExtractor>()
            .Bind<IArchiveFileWriter>()
            .To<ArchiveFileWriter>()
            .Bind()
            .To<ClientServices>()
            .Root<ClientServices>("Services");
}

internal static class ClientDefaults
{
    public static SharpCompressClient Client { get; } = new();
}
