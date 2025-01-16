using System;
using System.IO;
using System.Linq;
using SharpCompress.IO;

namespace SharpCompress.Common.SevenZip;

internal class SevenZipFilePart : FilePart
{
    private CompressionType? _type;
    private readonly Stream _stream;
    private readonly ArchiveDatabase _database;

    internal SevenZipFilePart(
        Stream stream,
        ArchiveDatabase database,
        int index,
        CFileItem fileEntry,
        ArchiveEncoding archiveEncoding
    )
        : base(archiveEncoding)
    {
        _stream = stream;
        _database = database;
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
            throw new InvalidOperationException("File does not have a stream.");
        }
        var folderStream = _database.GetFolderStream(_stream, Folder!, _database.PasswordProvider);

        var firstFileIndex = _database._folderStartFileIndex[_database._folders.IndexOf(Folder!)];
        var skipCount = Index - firstFileIndex;
        long skipSize = 0;
        for (var i = 0; i < skipCount; i++)
        {
            skipSize += _database._files[firstFileIndex + i].Size;
        }
        if (skipSize > 0)
        {
            folderStream.Skip(skipSize);
        }
        return new ReadOnlySubStream(folderStream, Header.Size);
    }

    public CompressionType CompressionType
    {
        get
        {
            _type ??= GetCompression();
            return _type.Value;
        }
    }

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

        var coder = Folder.NotNull()._coders.First();
        return coder._methodId._id switch
        {
            K_LZMA or K_LZMA2 => CompressionType.LZMA,
            K_PPMD => CompressionType.PPMd,
            K_B_ZIP2 => CompressionType.BZip2,
            _ => throw new NotImplementedException(),
        };
    }

    internal bool IsEncrypted =>
        !Header.IsDir
        && Folder?._coders.FindIndex(c => c._methodId._id == CMethodId.K_AES_ID) != -1;
}
