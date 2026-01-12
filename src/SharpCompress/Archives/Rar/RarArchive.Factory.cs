using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using SharpCompress.Common;
using SharpCompress.Common.Rar;
using SharpCompress.Common.Rar.Headers;
using SharpCompress.Compressors.Rar;
using SharpCompress.IO;
using SharpCompress.Readers;
using SharpCompress.Readers.Rar;

namespace SharpCompress.Archives.Rar;

public partial class RarArchive
{
    public static IRarArchive Open(string filePath, ReaderOptions? options = null)
    {
        filePath.NotNullOrEmpty(nameof(filePath));
        var fileInfo = new FileInfo(filePath);
        return new RarArchive(
            new SourceStream(
                fileInfo,
                i => RarArchiveVolumeFactory.GetFilePart(i, fileInfo),
                options ?? new ReaderOptions()
            )
        );
    }

    public static IRarArchive Open(FileInfo fileInfo, ReaderOptions? options = null)
    {
        fileInfo.NotNull(nameof(fileInfo));
        return new RarArchive(
            new SourceStream(
                fileInfo,
                i => RarArchiveVolumeFactory.GetFilePart(i, fileInfo),
                options ?? new ReaderOptions()
            )
        );
    }

    public static IRarArchive Open(Stream stream, ReaderOptions? options = null)
    {
        stream.NotNull(nameof(stream));

        if (stream is not { CanSeek: true })
        {
            throw new ArgumentException("Stream must be seekable", nameof(stream));
        }

        return new RarArchive(new SourceStream(stream, _ => null, options ?? new ReaderOptions()));
    }

    public static IRarArchive Open(
        IEnumerable<FileInfo> fileInfos,
        ReaderOptions? readerOptions = null
    )
    {
        fileInfos.NotNull(nameof(fileInfos));
        var files = fileInfos.ToArray();
        return new RarArchive(
            new SourceStream(
                files[0],
                i => i < files.Length ? files[i] : null,
                readerOptions ?? new ReaderOptions()
            )
        );
    }

    public static IRarArchive Open(IEnumerable<Stream> streams, ReaderOptions? readerOptions = null)
    {
        streams.NotNull(nameof(streams));
        var strms = streams.ToArray();
        return new RarArchive(
            new SourceStream(
                strms[0],
                i => i < strms.Length ? strms[i] : null,
                readerOptions ?? new ReaderOptions()
            )
        );
    }

    public static IRarAsyncArchive OpenAsync(
        Stream stream,
        ReaderOptions? readerOptions = null,
        CancellationToken cancellationToken = default
    )
    {
        cancellationToken.ThrowIfCancellationRequested();
        return (IRarAsyncArchive)Open(stream, readerOptions);
    }

    public static IRarAsyncArchive OpenAsync(
        FileInfo fileInfo,
        ReaderOptions? readerOptions = null,
        CancellationToken cancellationToken = default
    )
    {
        cancellationToken.ThrowIfCancellationRequested();
        return (IRarAsyncArchive)Open(fileInfo, readerOptions);
    }

    public static IRarAsyncArchive OpenAsync(
        IReadOnlyList<Stream> streams,
        ReaderOptions? readerOptions = null,
        CancellationToken cancellationToken = default
    )
    {
        cancellationToken.ThrowIfCancellationRequested();
        return (IRarAsyncArchive)Open(streams, readerOptions);
    }

    public static IRarAsyncArchive OpenAsync(
        IReadOnlyList<FileInfo> fileInfos,
        ReaderOptions? readerOptions = null,
        CancellationToken cancellationToken = default
    )
    {
        cancellationToken.ThrowIfCancellationRequested();
        return (IRarAsyncArchive)Open(fileInfos, readerOptions);
    }

    public static bool IsRarFile(string filePath) => IsRarFile(new FileInfo(filePath));

    public static bool IsRarFile(FileInfo fileInfo)
    {
        if (!fileInfo.Exists)
        {
            return false;
        }
        using Stream stream = fileInfo.OpenRead();
        return IsRarFile(stream);
    }

    public static bool IsRarFile(Stream stream, ReaderOptions? options = null)
    {
        try
        {
            MarkHeader.Read(stream, true, false);
            return true;
        }
        catch
        {
            return false;
        }
    }
}
