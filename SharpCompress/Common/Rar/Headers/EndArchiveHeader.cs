using SharpCompress.IO;

namespace SharpCompress.Common.Rar.Headers
{
    internal class EndArchiveHeader : RarHeader
    {
        protected override void ReadFromReader(MarkingBinaryReader reader)
        {
            if (EndArchiveFlags_HasFlag( EndArchiveFlags.EARC_DATACRC))
            {
                ArchiveCRC = reader.ReadInt32();
            }
            if (EndArchiveFlags_HasFlag(EndArchiveFlags.EARC_VOLNUMBER))
            {
                VolumeNumber = reader.ReadInt16();
            }
        }

        private bool EndArchiveFlags_HasFlag(EndArchiveFlags endArchiveFlags) {
            return (EndArchiveFlags & endArchiveFlags) == endArchiveFlags;
        }

        internal EndArchiveFlags EndArchiveFlags
        {
            get { return (EndArchiveFlags) base.Flags; }
        }

        internal int? ArchiveCRC { get; private set; }

        internal short? VolumeNumber { get; private set; }
    }
}