using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using SharpCompress.Common;
using SharpCompress.Common.Arj;
using SharpCompress.Common.Arj.Headers;
using SharpCompress.Common.Zip;
using SharpCompress.Common.Zip.Headers;

namespace SharpCompress.Readers.Arj
{
    public class ArjReader : AbstractReader<ArjEntry, ArjVolume>
    {
        private ArjReader(Stream stream, ReaderOptions options)
            : base(options, ArchiveType.Arj) => Volume = new ArjVolume(stream, options, 0);

        public override ArjVolume Volume { get; }

        /// <summary>
        /// Opens an ArjReader for Non-seeking usage with a single volume
        /// </summary>
        /// <param name="stream"></param>
        /// <param name="options"></param>
        /// <returns></returns>
        public static ArjReader Open(Stream stream, ReaderOptions? options = null)
        {
            stream.NotNull(nameof(stream));
            return new ArjReader(stream, options ?? new ReaderOptions());
        }

        protected override IEnumerable<ArjEntry> GetEntries(Stream stream)
        {
            ArchiveEncoding encoding = new ArchiveEncoding();
            var mainHeaderReader = new ArjMainHeader(encoding);
            var localHeaderReader = new ArjLocalHeader(encoding);
            var mainHeader = mainHeaderReader.Read(stream);

            while (true)
            {
                var localHeader = localHeaderReader.Read(stream);
                if (localHeader == null)
                    break;

                yield return new ArjEntry(new ArjFilePart((ArjLocalHeader)localHeader, stream));
            }
        }
    }
}
