using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using SharpCompress.Common;
using SharpCompress.Readers;

namespace SharpCompress.Archives;

public static partial class ArchiveFactory
{
    public static async ValueTask<IAsyncArchive> OpenAsyncArchive(
        Stream stream,
        ReaderOptions? readerOptions = null,
        CancellationToken cancellationToken = default
    )
    {
        readerOptions ??= ReaderOptions.ForExternalStream;
        var factory = await FindFactoryAsync<IArchiveFactory>(stream, cancellationToken)
            .ConfigureAwait(false);
        return await factory
            .OpenAsyncArchive(stream, readerOptions, cancellationToken)
            .ConfigureAwait(false);
    }

    public static ValueTask<IAsyncArchive> OpenAsyncArchive(
        string filePath,
        ReaderOptions? options = null,
        CancellationToken cancellationToken = default
    )
    {
        filePath.NotNullOrEmpty(nameof(filePath));
        return OpenAsyncArchive(
            new FileInfo(filePath),
            options ?? ReaderOptions.ForFilePath,
            cancellationToken
        );
    }

    public static async ValueTask<IAsyncArchive> OpenAsyncArchive(
        FileInfo fileInfo,
        ReaderOptions? options = null,
        CancellationToken cancellationToken = default
    )
    {
        options ??= ReaderOptions.ForFilePath;

        var factory = await FindFactoryAsync<IArchiveFactory>(fileInfo, cancellationToken)
            .ConfigureAwait(false);
        return await factory
            .OpenAsyncArchive(fileInfo, options, cancellationToken)
            .ConfigureAwait(false);
    }

    public static async ValueTask<IAsyncArchive> OpenAsyncArchive(
        IReadOnlyList<FileInfo> fileInfos,
        ReaderOptions? options = null,
        CancellationToken cancellationToken = default
    )
    {
        fileInfos.NotNull(nameof(fileInfos));
        var filesArray = fileInfos;
        if (filesArray.Count == 0)
        {
            throw new ArchiveOperationException("No files to open");
        }

        var fileInfo = filesArray[0];
        if (filesArray.Count == 1)
        {
            return await OpenAsyncArchive(fileInfo, options, cancellationToken)
                .ConfigureAwait(false);
        }

        fileInfo.NotNull(nameof(fileInfo));
        options ??= ReaderOptions.ForFilePath;

        var factory = await FindFactoryAsync<IMultiArchiveFactory>(fileInfo, cancellationToken)
            .ConfigureAwait(false);
        return await factory
            .OpenAsyncArchive(filesArray, options, cancellationToken)
            .ConfigureAwait(false);
    }

    public static async ValueTask<IAsyncArchive> OpenAsyncArchive(
        IReadOnlyList<Stream> streams,
        ReaderOptions? options = null,
        CancellationToken cancellationToken = default
    )
    {
        cancellationToken.ThrowIfCancellationRequested();
        var streamsArray = streams.RequireReadable().RequireSeekable().ToList();
        var firstStream = streamsArray[0];
        if (streamsArray.Count == 1)
        {
            return await OpenAsyncArchive(firstStream, options, cancellationToken)
                .ConfigureAwait(false);
        }

        firstStream.NotNull(nameof(firstStream));
        options ??= ReaderOptions.ForExternalStream;

        var factory = await FindFactoryAsync<IMultiArchiveFactory>(firstStream, cancellationToken)
            .ConfigureAwait(false);
        return await factory
            .OpenAsyncArchive(streamsArray, options, cancellationToken)
            .ConfigureAwait(false);
    }
}
