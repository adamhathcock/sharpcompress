using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using SharpCompress.Common;
using SharpCompress.Common.Ace;
using SharpCompress.Common.Ace.Headers;
using SharpCompress.Common.Arc;
using SharpCompress.Common.Arj;
using SharpCompress.Common.Arj.Headers;
using SharpCompress.Common.Rar.Headers;

namespace SharpCompress.Readers.Ace
{
    /// <summary>
    /// Reader for ACE archives.
    /// ACE is a proprietary archive format. This implementation supports both ACE 1.0 and ACE 2.0 formats
    /// and can read archive metadata and extract uncompressed (stored) entries.
    /// Compressed entries require proprietary decompression algorithms that are not publicly documented.
    /// </summary>
    /// <remarks>
    /// ACE 2.0 additions over ACE 1.0:
    /// - Improved LZ77 compression (compression type 2)
    /// - Recovery record support
    /// - Additional header flags
    /// </remarks>
    public class AceReader : AbstractReader<AceEntry, AceVolume>
    {
        private readonly AceMainHeader _mainHeaderReader;

        private AceReader(Stream stream, ReaderOptions options)
            : base(options, ArchiveType.Ace)
        {
            Volume = new AceVolume(stream, options, 0);
            _mainHeaderReader = new AceMainHeader(Options.ArchiveEncoding);
        }

        public override AceVolume Volume { get; }

        /// <summary>
        /// Opens an AceReader for non-seeking usage with a single volume.
        /// </summary>
        /// <param name="stream">The stream containing the ACE archive.</param>
        /// <param name="options">Reader options.</param>
        /// <returns>An AceReader instance.</returns>
        public static AceReader Open(Stream stream, ReaderOptions? options = null)
        {
            stream.NotNull(nameof(stream));
            return new AceReader(stream, options ?? new ReaderOptions());
        }

        protected override IEnumerable<AceEntry> GetEntries(Stream stream)
        {
            var mainHeader = _mainHeaderReader.Read(stream);
            if (mainHeader == null)
            {
                yield break;
            }

            if (mainHeader?.IsMultiVolume == true)
            {
                throw new MultiVolumeExtractionException(
                    "Multi volumes are currently not supported"
                );
            }
            if (mainHeader?.IsEncrypted == true)
            {
                throw new CryptographicException(
                    "Password protected archives are currently not supported"
                );
            }

            var localHeaderReader = new AceFileHeader(base.Options.ArchiveEncoding);
            while (true)
            {
                var localHeader = localHeaderReader.Read(stream);
                if (localHeader == null)
                    break;

                yield return new AceEntry(new AceFilePart((AceFileHeader)localHeader, stream));
            }
        }
    }
}
