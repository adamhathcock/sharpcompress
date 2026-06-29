using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using SharpCompress.Archives;
using SharpCompress.Common;
using SharpCompress.Readers;

namespace SharpCompress.Factories;

internal static class FactoryDetection
{
    internal static T FindFactory<T>(string filePath)
        where T : IFactory
    {
        filePath.NotNullOrEmpty(nameof(filePath));
        using Stream stream = File.OpenRead(filePath);
        return FindFactory<T>(stream);
    }

    internal static T FindFactory<T>(FileInfo fileInfo)
        where T : IFactory
    {
        fileInfo.NotNull(nameof(fileInfo));
        using Stream stream = fileInfo.OpenRead();
        return FindFactory<T>(stream);
    }

    internal static T FindFactory<T>(Stream stream)
        where T : IFactory
    {
        stream.RequireReadable();
        stream.RequireSeekable();

        var target = GetDetectionTarget<T>();
        var match = Detect(stream, ReaderOptions.ForExternalStream, target);
        if (match.Result == FactoryDetectionResult.Match && match.Factory is T typedFactory)
        {
            return typedFactory;
        }

        var extensions = string.Join(", ", Factory.Factories.OfType<T>().Select(item => item.Name));

        throw new ArchiveOperationException(
            $"Cannot determine compressed stream type. Supported Archive Formats: {extensions}"
        );
    }

    internal static ValueTask<T> FindFactoryAsync<T>(
        string filePath,
        CancellationToken cancellationToken = default
    )
        where T : IFactory
    {
        filePath.NotNullOrEmpty(nameof(filePath));
        return FindFactoryAsync<T>(new FileInfo(filePath), cancellationToken);
    }

    internal static async ValueTask<T> FindFactoryAsync<T>(
        FileInfo fileInfo,
        CancellationToken cancellationToken = default
    )
        where T : IFactory
    {
        fileInfo.NotNull(nameof(fileInfo));
        using Stream stream = fileInfo.OpenRead();
        return await FindFactoryAsync<T>(stream, cancellationToken).ConfigureAwait(false);
    }

    internal static async ValueTask<T> FindFactoryAsync<T>(
        Stream stream,
        CancellationToken cancellationToken = default
    )
        where T : IFactory
    {
        stream.RequireReadable();
        stream.RequireSeekable();

        var target = GetDetectionTarget<T>();
        var match = await DetectAsync(
                stream,
                ReaderOptions.ForExternalStream,
                target,
                cancellationToken
            )
            .ConfigureAwait(false);
        if (match.Result == FactoryDetectionResult.Match && match.Factory is T typedFactory)
        {
            return typedFactory;
        }

        var extensions = string.Join(", ", Factory.Factories.OfType<T>().Select(item => item.Name));

        throw new ArchiveOperationException(
            $"Cannot determine compressed stream type. Supported Archive Formats: {extensions}"
        );
    }

    internal static FactoryDetectionMatch Detect(
        Stream stream,
        ReaderOptions readerOptions,
        FactoryDetectionTarget target
    )
    {
        var startPosition = stream.Position;

        foreach (var factory in Factory.Factories.OfType<Factory>())
        {
            stream.Seek(startPosition, SeekOrigin.Begin);
            var match = factory.DetectMatch(stream, readerOptions, target);
            if (match.Result != FactoryDetectionResult.NoMatch)
            {
                stream.Seek(startPosition, SeekOrigin.Begin);
                return match;
            }
        }

        stream.Seek(startPosition, SeekOrigin.Begin);
        return new FactoryDetectionMatch(FactoryDetectionResult.NoMatch, null, null);
    }

    internal static async ValueTask<FactoryDetectionMatch> DetectAsync(
        Stream stream,
        ReaderOptions readerOptions,
        FactoryDetectionTarget target,
        CancellationToken cancellationToken = default
    )
    {
        var startPosition = stream.Position;

        foreach (var factory in Factory.Factories.OfType<Factory>())
        {
            stream.Seek(startPosition, SeekOrigin.Begin);
            var match = await factory
                .DetectMatchAsync(stream, readerOptions, target, cancellationToken)
                .ConfigureAwait(false);
            if (match.Result != FactoryDetectionResult.NoMatch)
            {
                stream.Seek(startPosition, SeekOrigin.Begin);
                return match;
            }
        }

        stream.Seek(startPosition, SeekOrigin.Begin);
        return new FactoryDetectionMatch(FactoryDetectionResult.NoMatch, null, null);
    }

    private static FactoryDetectionTarget GetDetectionTarget<T>()
        where T : IFactory
    {
        var factoryType = typeof(T);
        if (
            typeof(IArchiveFactory).IsAssignableFrom(factoryType)
            || typeof(IMultiArchiveFactory).IsAssignableFrom(factoryType)
        )
        {
            return FactoryDetectionTarget.Archive;
        }

        return typeof(IReaderFactory).IsAssignableFrom(factoryType)
            ? FactoryDetectionTarget.Reader
            : FactoryDetectionTarget.Identify;
    }
}
