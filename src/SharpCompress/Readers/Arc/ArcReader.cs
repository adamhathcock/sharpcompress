using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using SharpCompress.Common;
using SharpCompress.Common.Arc;

namespace SharpCompress.Readers.Arc
{
    public class ArcReader : AbstractReader<ArcEntry, ArcVolume>
    {
        private ArcReader(Stream stream, ReaderOptions options)
            : base(options, ArchiveType.Arc) => Volume = new ArcVolume(stream, options, 0);

        public override ArcVolume Volume { get; }

        /// <summary>
        /// Opens an ArcReader for Non-seeking usage with a single volume
        /// </summary>
        /// <param name="stream"></param>
        /// <param name="options"></param>
        /// <returns></returns>
        public static ArcReader Open(Stream stream, ReaderOptions? options = null)
        {
            stream.NotNull(nameof(stream));
            return new ArcReader(stream, options ?? new ReaderOptions());
        }

        protected override IEnumerable<ArcEntry> GetEntries(Stream stream)
        {
            ArcEntryHeader headerReader = new ArcEntryHeader(Options.ArchiveEncoding);
            ArcEntryHeader? header;
            while ((header = headerReader.ReadHeader(stream)) != null)
            {
                yield return new ArcEntry(new ArcFilePart(header, stream));
            }
        }
    }
}
