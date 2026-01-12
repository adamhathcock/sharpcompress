using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using SharpCompress.Common;
using SharpCompress.Common.SevenZip;
using SharpCompress.Compressors.LZMA.Utilites;
using SharpCompress.IO;
using SharpCompress.Readers;

namespace SharpCompress.Archives.SevenZip;

public partial class SevenZipArchive
{
    public static IArchive Open(string filePath, ReaderOptions? readerOptions = null)
    {
        filePath.NotNullOrEmpty("filePath");
        return Open(new FileInfo(filePath), readerOptions ?? new ReaderOptions());
    }

    public static IArchive Open(FileInfo fileInfo, ReaderOptions? readerOptions = null)
    {
        fileInfo.NotNull("fileInfo");
        return new SevenZipArchive(
            new SourceStream(
                fileInfo,
                i => ArchiveVolumeFactory.GetFilePart(i, fileInfo),
                readerOptions ?? new ReaderOptions()
            )
        );
    }

    public static IArchive Open(
        IEnumerable<FileInfo> fileInfos,
        ReaderOptions? readerOptions = null
    )
    {
        fileInfos.NotNull(nameof(fileInfos));
        var files = fileInfos.ToArray();
        return new SevenZipArchive(
            new SourceStream(
                files[0],
                i => i < files.Length ? files[i] : null,
                readerOptions ?? new ReaderOptions()
            )
        );
    }

    public static IArchive Open(IEnumerable<Stream> streams, ReaderOptions? readerOptions = null)
    {
        streams.NotNull(nameof(streams));
        var strms = streams.ToArray();
        return new SevenZipArchive(
            new SourceStream(
                strms[0],
                i => i < strms.Length ? strms[i] : null,
                readerOptions ?? new ReaderOptions()
            )
        );
    }

    public static IArchive Open(Stream stream, ReaderOptions? readerOptions = null)
    {
        stream.NotNull("stream");

        if (stream is not { CanSeek: true })
        {
            throw new ArgumentException("Stream must be seekable", nameof(stream));
        }

        return new SevenZipArchive(
            new SourceStream(stream, _ => null, readerOptions ?? new ReaderOptions())
        );
    }

    public static IAsyncArchive OpenAsync(
        Stream stream,
        ReaderOptions? readerOptions = null,
        CancellationToken cancellationToken = default
    )
    {
        cancellationToken.ThrowIfCancellationRequested();
        return (IAsyncArchive)Open(stream, readerOptions);
    }

    public static IAsyncArchive OpenAsync(
        FileInfo fileInfo,
        ReaderOptions? readerOptions = null,
        CancellationToken cancellationToken = default
    )
    {
        cancellationToken.ThrowIfCancellationRequested();
        return (IAsyncArchive)Open(fileInfo, readerOptions);
    }

    public static IAsyncArchive OpenAsync(
        IReadOnlyList<Stream> streams,
        ReaderOptions? readerOptions = null,
        CancellationToken cancellationToken = default
    )
    {
        cancellationToken.ThrowIfCancellationRequested();
        return (IAsyncArchive)Open(streams, readerOptions);
    }

    public static IAsyncArchive OpenAsync(
        IReadOnlyList<FileInfo> fileInfos,
        ReaderOptions? readerOptions = null,
        CancellationToken cancellationToken = default
    )
    {
        cancellationToken.ThrowIfCancellationRequested();
        return (IAsyncArchive)Open(fileInfos, readerOptions);
    }

    public static bool IsSevenZipFile(string filePath) => IsSevenZipFile(new FileInfo(filePath));

    public static bool IsSevenZipFile(FileInfo fileInfo)
    {
        if (!fileInfo.Exists)
        {
            return false;
        }
        using Stream stream = fileInfo.OpenRead();
        return IsSevenZipFile(stream);
    }

    public static bool IsSevenZipFile(Stream stream)
    {
        try
        {
            return SignatureMatch(stream);
        }
        catch
        {
            return false;
        }
    }

    private static ReadOnlySpan<byte> Signature =>
        new byte[] { (byte)'7', (byte)'z', 0xBC, 0xAF, 0x27, 0x1C };

    private static bool SignatureMatch(Stream stream)
    {
        var reader = new BinaryReader(stream);
        ReadOnlySpan<byte> signatureBytes = reader.ReadBytes(6);
        return signatureBytes.SequenceEqual(Signature);
    }
}
