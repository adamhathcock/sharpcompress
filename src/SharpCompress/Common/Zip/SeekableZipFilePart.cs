using System.IO;
using SharpCompress.Common.Zip.Headers;
using SharpCompress.Providers;

namespace SharpCompress.Common.Zip;

internal partial class SeekableZipFilePart : ZipFilePart
{
    private bool _isLocalHeaderLoaded;
    private readonly SeekableZipHeaderFactory _headerFactory;
    private readonly DirectoryEntryHeader _directoryEntryHeader;

    internal SeekableZipFilePart(
        SeekableZipHeaderFactory headerFactory,
        DirectoryEntryHeader header,
        Stream stream,
        CompressionProviderRegistry compressionProviders
    )
        : base(header, stream, compressionProviders)
    {
        _headerFactory = headerFactory;
        _directoryEntryHeader = header;
    }

    internal LocalEntryHeader GetRawLocalHeader() =>
        _headerFactory.GetRawLocalHeader(BaseStream, _directoryEntryHeader);

    internal bool HasDeferredSizes =>
        FlagUtility.HasFlag(_directoryEntryHeader.Flags, HeaderFlags.UsePostDataDescriptor);

    protected override Stream CreateBaseStream()
    {
        BaseStream.Position = Header.DataStartPosition.NotNull();

        return BaseStream;
    }
}
