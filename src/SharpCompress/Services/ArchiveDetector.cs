using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Pure.DI;
using SharpCompress.Common;
using SharpCompress.Factories;
using SharpCompress.Readers;

namespace SharpCompress.Services;

[GenerateInterface(asInternal: true)]
internal sealed partial class ArchiveDetector(IFactoryCatalog catalog) : IArchiveDetector
{
    public T FindFactory<T>(string filePath)
        where T : IFactory
    {
        filePath.NotNullOrEmpty(nameof(filePath));
        using Stream stream = File.OpenRead(filePath);
        return FindFactory<T>(stream);
    }

    public T FindFactory<T>(FileInfo finfo)
        where T : IFactory
    {
        finfo.NotNull(nameof(finfo));
        using Stream stream = finfo.OpenRead();
        return FindFactory<T>(stream);
    }

    public T FindFactory<T>(Stream stream)
        where T : IFactory => FindFactory<T>(stream, ReaderOptions.ForExternalStream);

    public bool IsArchive(string filePath, out ArchiveType? type) =>
        IsArchive(filePath, ReaderOptions.ForFilePath, out type);

    public bool IsArchive(string filePath, ReaderOptions? readerOptions, out ArchiveType? type)
    {
        filePath.NotNullOrEmpty(nameof(filePath));
        using Stream stream = File.OpenRead(filePath);
        return IsArchive(stream, readerOptions ?? ReaderOptions.ForFilePath, out type);
    }

    public bool IsArchive(Stream stream, out ArchiveType? type) =>
        IsArchive(stream, ReaderOptions.ForExternalStream, out type);

    public bool IsArchive(Stream stream, ReaderOptions? readerOptions, out ArchiveType? type)
    {
        stream.RequireReadable();
        stream.RequireSeekable();
        var factory = TryFindFactory(stream, readerOptions ?? ReaderOptions.ForExternalStream);
        type = factory?.KnownArchiveType;
        return factory is not null;
    }

    public async ValueTask<(bool IsArchive, ArchiveType? Type)> IsArchiveAsync(
        string filePath,
        CancellationToken cancellationToken = default
    ) =>
        await IsArchiveAsync(filePath, ReaderOptions.ForFilePath, cancellationToken)
            .ConfigureAwait(false);

    public async ValueTask<(bool IsArchive, ArchiveType? Type)> IsArchiveAsync(
        string filePath,
        ReaderOptions? readerOptions,
        CancellationToken cancellationToken = default
    )
    {
        filePath.NotNullOrEmpty(nameof(filePath));
        using Stream stream = File.OpenRead(filePath);
        return await IsArchiveAsync(
                stream,
                readerOptions ?? ReaderOptions.ForFilePath,
                cancellationToken
            )
            .ConfigureAwait(false);
    }

    public async ValueTask<(bool IsArchive, ArchiveType? Type)> IsArchiveAsync(
        Stream stream,
        CancellationToken cancellationToken = default
    ) =>
        await IsArchiveAsync(stream, ReaderOptions.ForExternalStream, cancellationToken)
            .ConfigureAwait(false);

    public async ValueTask<(bool IsArchive, ArchiveType? Type)> IsArchiveAsync(
        Stream stream,
        ReaderOptions? readerOptions,
        CancellationToken cancellationToken = default
    )
    {
        stream.RequireReadable();
        stream.RequireSeekable();
        var factory = await TryFindFactoryAsync(
                stream,
                readerOptions ?? ReaderOptions.ForExternalStream,
                cancellationToken
            )
            .ConfigureAwait(false);
        return (factory is not null, factory?.KnownArchiveType);
    }
}
