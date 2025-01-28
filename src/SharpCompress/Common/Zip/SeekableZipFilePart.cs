using System.IO;
using SharpCompress.Common.Zip.Headers;

namespace SharpCompress.Common.Zip;

internal class SeekableZipFilePart : ZipFilePart
{
    private bool _isLocalHeaderLoaded;
    private readonly SeekableZipHeaderFactory _headerFactory;

    internal SeekableZipFilePart(
        SeekableZipHeaderFactory headerFactory,
        DirectoryEntryHeader header,
        Stream stream
    )
        : base(header, stream) => _headerFactory = headerFactory;

    internal override Stream GetCompressedStream()
    {
        if (!_isLocalHeaderLoaded)
        {
            LoadLocalHeader();
            _isLocalHeaderLoaded = true;
        }
        return base.GetCompressedStream();
    }

    internal string? Comment => ((DirectoryEntryHeader)Header).Comment;

    private void LoadLocalHeader()
    {
        var hasData = Header.HasData;
        Header = _headerFactory.GetLocalHeader(BaseStream, ((DirectoryEntryHeader)Header));
        Header.HasData = hasData;
    }

    protected override Stream CreateBaseStream()
    {
        BaseStream.Position = Header.DataStartPosition.NotNull();

        return BaseStream;
    }
}
