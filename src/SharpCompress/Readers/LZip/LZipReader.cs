using System.Collections.Generic;
using System.IO;
using SharpCompress.Common;
using SharpCompress.Common.LZip;

namespace SharpCompress.Readers.LZip
{
    public class LZipReader : AbstractReader<LZipEntry, LZipVolume>
    {
        internal LZipReader(Stream stream, ReaderOptions options)
            : base(options, ArchiveType.LZip)
        {
            Volume = new LZipVolume(stream, options);
        }

        public override LZipVolume Volume { get; }

        #region Open

        /// <summary>
        /// Opens a GZipReader for Non-seeking usage with a single volume
        /// </summary>
        /// <param name="stream"></param>
        /// <param name="options"></param>
        /// <returns></returns>
        public static LZipReader Open(Stream stream, ReaderOptions options = null)
        {
            stream.CheckNotNull("stream");
            return new LZipReader(stream, options ?? new ReaderOptions());
        }

        #endregion

        internal override IEnumerable<LZipEntry> GetEntries(Stream stream)
        {
            return LZipEntry.GetEntries(stream);
        }
    }
}