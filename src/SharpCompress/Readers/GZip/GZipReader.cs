using System.Collections.Generic;
using System.IO;
using SharpCompress.Common;
using SharpCompress.Common.GZip;

namespace SharpCompress.Readers.GZip
{
    public class GZipReader : AbstractReader<GZipEntry, GZipVolume>
    {
        internal GZipReader(Stream stream, Options options)
            : base(options, ArchiveType.GZip)
        {
            Volume = new GZipVolume(stream, options);
        }

        public override GZipVolume Volume { get; }

        #region Open

        /// <summary>
        /// Opens a GZipReader for Non-seeking usage with a single volume
        /// </summary>
        /// <param name="stream"></param>
        /// <param name="options"></param>
        /// <returns></returns>
        public static GZipReader Open(Stream stream,
                                      Options options = Options.KeepStreamsOpen)
        {
            stream.CheckNotNull("stream");
            return new GZipReader(stream, options);
        }

        #endregion

        internal override IEnumerable<GZipEntry> GetEntries(Stream stream)
        {
            return GZipEntry.GetEntries(stream);
        }
    }
}