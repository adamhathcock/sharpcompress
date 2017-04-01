using SharpCompress.IO;

namespace SharpCompress.Common.Rar.Headers
{
    internal class EndArchiveHeader : RarHeader
    {
        protected override void ReadFromReader(MarkingBinaryReader reader)
        {
            if (EndArchiveFlags.HasFlag(EndArchiveFlags.EARC_DATACRC))
            {
                ArchiveCRC = reader.ReadInt32();
            }
            if (EndArchiveFlags.HasFlag(EndArchiveFlags.EARC_VOLNUMBER))
            {
                VolumeNumber = reader.ReadInt16();
            }
        }

        internal EndArchiveFlags EndArchiveFlags
        {
            get
            {
                return (EndArchiveFlags)base.Flags;
            }
        }

        internal int? ArchiveCRC
        {
            get;
            private set;
        }

        internal short? VolumeNumber
        {
            get;
            private set;
        }
    }
}
