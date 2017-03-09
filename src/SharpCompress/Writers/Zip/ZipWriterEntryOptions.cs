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

        /// <summary>
        /// Allocate space for storing values if the file is larger than 4GiB
        /// </summary>
        public bool? EnableZip64 { get; set; }
    }
}