using System;
using SharpCompress.Common;
using SharpCompress.Compressors.Deflate;

namespace SharpCompress.Writers.Zip
{
    public class ZipWriterEntryOptions
    {
        public CompressionType? CompressionType { get; set; }
        /// <summary>
        /// When CompressionType.Deflate is used, this property is referenced.  Defaults to CompressionLevel.Default.
        /// </summary>
        public CompressionLevel? DeflateCompressionLevel { get; set; }

        public string EntryComment { get; set; }

        public DateTime? ModificationDateTime { get; set; }
    }
}