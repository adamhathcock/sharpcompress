using System.Collections.Generic;
using SharpCompress.Common;
using SharpCompress.Common.Rar;
using SharpCompress.Common.Rar.Headers;

namespace SharpCompress.Reader.Rar
{
    public class RarReaderEntry : RarEntry
    {
        internal RarReaderEntry(bool solid, RarFilePart part)
        {
            Part = part;
            IsSolid = solid;
        }

        internal bool IsSolid { get; private set; }

        internal RarFilePart Part { get; private set; }

        internal override IEnumerable<FilePart> Parts
        {
            get { return Part.AsEnumerable<FilePart>(); }
        }

        internal override FileHeader FileHeader
        {
            get { return Part.FileHeader; }
        }

        public override CompressionType CompressionType
        {
            get { return CompressionType.Rar; }
        }

        /// <summary>
        /// The compressed file size
        /// </summary>
        public override long CompressedSize
        {
            get { return Part.FileHeader.CompressedSize; }
        }

        /// <summary>
        /// The uncompressed file size
        /// </summary>
        public override long Size
        {
            get { return Part.FileHeader.UncompressedSize; }
        }

        internal override void Close()
        {
        }
    }
}