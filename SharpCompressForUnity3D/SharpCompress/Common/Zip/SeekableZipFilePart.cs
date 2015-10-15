namespace SharpCompress.Common.Zip
{
    using SharpCompress.Common.Zip.Headers;
    using System;
    using System.IO;

    internal class SeekableZipFilePart : ZipFilePart
    {
        private readonly SeekableZipHeaderFactory headerFactory;
        private bool isLocalHeaderLoaded;

        internal SeekableZipFilePart(SeekableZipHeaderFactory headerFactory, DirectoryEntryHeader header, Stream stream) : base(header, stream)
        {
            this.headerFactory = headerFactory;
        }

        protected override Stream CreateBaseStream()
        {
            base.BaseStream.Position = base.Header.DataStartPosition.Value;
            return base.BaseStream;
        }

        internal override Stream GetCompressedStream()
        {
            if (!this.isLocalHeaderLoaded)
            {
                this.LoadLocalHeader();
                this.isLocalHeaderLoaded = true;
            }
            return base.GetCompressedStream();
        }

        private void LoadLocalHeader()
        {
            bool hasData = base.Header.HasData;
            base.Header = this.headerFactory.GetLocalHeader(base.BaseStream, base.Header as DirectoryEntryHeader);
            base.Header.HasData = hasData;
        }

        internal string Comment
        {
            get
            {
                return (base.Header as DirectoryEntryHeader).Comment;
            }
        }
    }
}

