using System.IO;
using System.Threading;
using System.Threading.Tasks;
using SharpCompress.IO;

namespace SharpCompress.Common.SevenZip;

internal class SevenZipFilePart : FilePart
{
    private CompressionType? _type;
    private readonly Stream _stream;
    private readonly ArchiveDatabase _database;
    private readonly bool _enableParallelism;

    internal SevenZipFilePart(
        Stream stream,
        ArchiveDatabase database,
        int index,
        CFileItem fileEntry,
        IArchiveEncoding archiveEncoding,
        bool enableParallelism = false
    )
        : base(archiveEncoding)
    {
        _stream = stream;
        _database = database;
        _enableParallelism = enableParallelism;
        Index = index;
        Header = fileEntry;
        if (Header.HasStream)
        {
            Folder = database._folders[database._fileIndexToFolderIndexMap[index]];
        }
    }

    internal CFileItem Header { get; }
    internal CFolder? Folder { get; }

    internal override string FilePartName => Header.Name;

    internal override Stream? GetRawStream() => null;

    internal override Stream GetCompressedStream()
    {
        if (!Header.HasStream)
        {
            return Stream.Null;
        }

        var firstFileIndex = _database._folderStartFileIndex[_database._folders.IndexOf(Folder!)];
        var skipCount = Index - firstFileIndex;
        long skipSize = 0;
        for (var i = 0; i < skipCount; i++)
        {
            skipSize += _database._files[firstFileIndex + i].Size;
        }

        var folderStream = _database.GetFolderStream(
            _stream,
            Folder!,
            _database.PasswordProvider,
            skipSize,
            Header.Size,
            _enableParallelism
        );
        return new ReadOnlySubStream(folderStream, Header.Size, leaveOpen: true);
    }

    internal override async ValueTask<Stream?> GetCompressedStreamAsync(
        CancellationToken cancellationToken = default
    )
    {
        if (!Header.HasStream)
        {
            return Stream.Null;
        }

        var firstFileIndex = _database._folderStartFileIndex[_database._folders.IndexOf(Folder!)];
        var skipCount = Index - firstFileIndex;
        long skipSize = 0;
        for (var i = 0; i < skipCount; i++)
        {
            skipSize += _database._files[firstFileIndex + i].Size;
        }

        var folderStream = await _database
            .GetFolderStreamAsync(
                _stream,
                Folder!,
                _database.PasswordProvider,
                skipSize,
                Header.Size,
                cancellationToken
            )
            .ConfigureAwait(false);
        return new ReadOnlySubStream(folderStream, Header.Size, leaveOpen: true);
    }

    public CompressionType CompressionType
    {
        get
        {
            _type ??= GetCompression();
            return _type.Value;
        }
    }

    private const uint K_COPY = 0x0;
    private const uint K_LZMA2 = 0x21;
    private const uint K_LZMA = 0x030101;
    private const uint K_PPMD = 0x030401;
    private const uint K_B_ZIP2 = 0x040202;

    private CompressionType GetCompression()
    {
        if (Header.IsDir)
        {
            return CompressionType.None;
        }

        // 7zAES is a filter in front of the codec (7-Zip writes it as coder 0), so it says
        // nothing about the compression; report the first real coder instead.
        var coders = Folder.NotNull()._coders;
        // Predicate-based LINQ boxes List's enumerator on older supported runtimes.
        for (var i = 0; i < coders.Count; i++)
        {
            var methodId = coders[i]._methodId._id;
            if (methodId == CMethodId.K_AES_ID)
            {
                continue;
            }
            return methodId switch
            {
                K_COPY => CompressionType.None,
                K_LZMA or K_LZMA2 => CompressionType.LZMA,
                K_PPMD => CompressionType.PPMd,
                K_B_ZIP2 => CompressionType.BZip2,
                _ => throw new InvalidFormatException(),
            };
        }
        throw new InvalidFormatException();
    }

    internal bool IsEncrypted => !Header.IsDir && Folder?.IsEncrypted() == true;
}
