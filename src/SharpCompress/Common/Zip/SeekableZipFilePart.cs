using System;
using System.IO;
using SharpCompress.Common.Zip.Headers;
using SharpCompress.IO;

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

    private void LoadLocalHeader() =>
        Header = _headerFactory.GetLocalHeader(BaseStream, (DirectoryEntryHeader)Header);

    protected override Stream CreateBaseStream()
    {
        if (BaseStream is SourceStream ss)
        {
            if (ss.IsFileMode)
            {
                var fileStream = ss.CurrentFile.OpenRead();
                fileStream.Position = Header.DataStartPosition.NotNull();
                return fileStream;
            }
        }
        BaseStream.Position = Header.DataStartPosition.NotNull();

        return BaseStream;
    }

    public override bool SupportsMultiThreading => BaseStream is SourceStream ss && ss.IsFileMode;

}
