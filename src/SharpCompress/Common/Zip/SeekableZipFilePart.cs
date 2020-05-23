using System.IO;
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
            bool hasData = Header.HasData;
            Header = _headerFactory.GetLocalHeader(BaseStream, ((DirectoryEntryHeader)Header));
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