using System.IO;
using System.Threading;
using System.Threading.Tasks;
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

    internal override async ValueTask<Stream?> GetCompressedStreamAsync(
        CancellationToken cancellationToken = default
    )
    {
        if (!_isLocalHeaderLoaded)
        {
            await LoadLocalHeaderAsync(cancellationToken);
            _isLocalHeaderLoaded = true;
        }
        return await base.GetCompressedStreamAsync(cancellationToken);
    }

    private void LoadLocalHeader() =>
        Header = _headerFactory.GetLocalHeader(BaseStream, (DirectoryEntryHeader)Header);

    private async ValueTask LoadLocalHeaderAsync(CancellationToken cancellationToken = default) =>
        Header = await _headerFactory.GetLocalHeaderAsync(BaseStream, (DirectoryEntryHeader)Header);

    protected override Stream CreateBaseStream()
    {
        BaseStream.Position = Header.DataStartPosition.NotNull();

        return BaseStream;
    }
}
