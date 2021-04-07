using System.IO;
using System.Threading;
using System.Threading.Tasks;
using SharpCompress.Common.Zip.Headers;
using SharpCompress.IO;

namespace SharpCompress.Common.Zip
{
    internal class SeekableZipFilePart : ZipFilePart
    {
        private bool _isLocalHeaderLoaded;
        private readonly SeekableZipHeaderFactory _headerFactory;
        private readonly DirectoryEntryHeader _directoryEntryHeader;

        internal SeekableZipFilePart(SeekableZipHeaderFactory headerFactory, DirectoryEntryHeader header, Stream stream)
            : base(header, stream)
        {
            _headerFactory = headerFactory;
            _directoryEntryHeader = header;
        }

        internal override async ValueTask<Stream> GetCompressedStreamAsync(CancellationToken cancellationToken)
        {
            if (!_isLocalHeaderLoaded)
            {
                await LoadLocalHeader(cancellationToken);
                _isLocalHeaderLoaded = true;
            }
            return await base.GetCompressedStreamAsync(cancellationToken);
        }

        internal string? Comment => ((DirectoryEntryHeader)Header).Comment;

        private async ValueTask LoadLocalHeader(CancellationToken cancellationToken)
        {
            bool hasData = Header.HasData;
            Header = await _headerFactory.GetLocalHeader(BaseStream, (DirectoryEntryHeader)Header, cancellationToken);
            Header.HasData = hasData;
        }

        protected override Stream CreateBaseStream()
        {
            BaseStream.Position = Header.DataStartPosition!.Value;

            if ((Header.CompressedSize == 0)
                && FlagUtility.HasFlag(Header.Flags, HeaderFlags.UsePostDataDescriptor)
                && (_directoryEntryHeader?.HasData == true)
                && (_directoryEntryHeader?.CompressedSize != 0))
            {
                return new ReadOnlySubStream(BaseStream, _directoryEntryHeader!.CompressedSize);
            }

            return BaseStream;
        }
    }
}