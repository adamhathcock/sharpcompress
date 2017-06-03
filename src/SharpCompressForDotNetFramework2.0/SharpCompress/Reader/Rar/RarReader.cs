using System.Collections.Generic;
using System.IO;
using System.Linq;
using SharpCompress.Common;
using SharpCompress.Common.Rar;
using SharpCompress.Compressor.Rar;

namespace SharpCompress.Reader.Rar
{
    /// <summary>
    /// This class faciliates Reading a Rar Archive in a non-seekable forward-only manner
    /// </summary>
    public abstract class RarReader : AbstractReader<RarReaderEntry, RarVolume>
    {
        private RarVolume volume;
        private readonly Unpack pack = new Unpack();

        internal RarReader(Options options)
            : base(options, ArchiveType.Rar)
        {
        }

        internal abstract void ValidateArchive(RarVolume archive);

        public override RarVolume Volume
        {
            get
            {
                return volume;
            }
        }

        #region Open
        /// <summary>
        /// Opens a RarReader for Non-seeking usage with a single volume
        /// </summary>
        /// <param name="stream"></param>
        /// <param name="options"></param>
        /// <returns></returns>
        public static RarReader Open(Stream stream, Options options = Options.KeepStreamsOpen)
        {
            stream.CheckNotNull("stream");
            return new SingleVolumeRarReader(stream, options);
        }

        /// <summary>
        /// Opens a RarReader for Non-seeking usage with multiple volumes
        /// </summary>
        /// <param name="streams"></param>
        /// <param name="options"></param>
        /// <returns></returns>
        public static RarReader Open(IEnumerable<Stream> streams, Options options = Options.KeepStreamsOpen)
        {
            streams.CheckNotNull("streams");
            return new MultiVolumeRarReader(streams, options);
        }
        #endregion

        internal override IEnumerable<RarReaderEntry> GetEntries(Stream stream)
        {
            volume = new RarReaderVolume(stream, Options);
            foreach (RarFilePart fp in volume.ReadFileParts())
            {
                ValidateArchive(volume);
                yield return new RarReaderEntry(volume.IsSolidArchive, fp);
            }
        }

        protected virtual IEnumerable<FilePart> CreateFilePartEnumerableForCurrentEntry()
        {
            return Entry.Parts;
        }

        protected override EntryStream GetEntryStream()
        {
            return new EntryStream(new RarStream(pack, Entry.FileHeader,
                                 new MultiVolumeReadOnlyStream(CreateFilePartEnumerableForCurrentEntry().Cast<RarFilePart>(), this)));
        }
    }
}
