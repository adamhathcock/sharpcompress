using System.IO;
using SharpCompress.Common.Zip.Headers;
using SharpCompress.IO;

namespace SharpCompress.Common.Zip;

internal class SeekableZipFilePart : ZipFilePart
{
    private bool _isLocalHeaderLoaded;
    private readonly SeekableZipHeaderFactory _headerFactory;
    private readonly bool _isMultiVolume;

    internal SeekableZipFilePart(
        SeekableZipHeaderFactory headerFactory,
        DirectoryEntryHeader header,
        Stream stream,
        bool isMultiVolume
    )
        : base(header, stream)
    {
        _headerFactory = headerFactory;
        _isMultiVolume = isMultiVolume;
    }

    internal override Stream GetCompressedStream()
    {
        if (!_isLocalHeaderLoaded)
        {
            LoadLocalHeader();
            _isLocalHeaderLoaded = true;
        }
        return base.GetCompressedStream();
    }

    private void LoadLocalHeader() =>
        Header = _headerFactory.GetLocalHeader(BaseStream, (DirectoryEntryHeader)Header);

    protected override Stream CreateBaseStream()
    {
        if (!_isMultiVolume && BaseStream is SourceStream ss)
        {
            if (ss.IsFileMode && ss.Files.Count == 1)
            {
                var fileStream = ss.CurrentFile.OpenRead();
                fileStream.Position = Header.DataStartPosition.NotNull();
                return fileStream;
            }
        }
        BaseStream.Position = Header.DataStartPosition.NotNull();

        return BaseStream;
    }

    public override bool SupportsMultiThreading =>
        !_isMultiVolume && BaseStream is SourceStream ss && ss.IsFileMode && ss.Files.Count == 1;
}
