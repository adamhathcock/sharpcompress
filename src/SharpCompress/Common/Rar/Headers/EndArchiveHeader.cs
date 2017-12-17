using SharpCompress.IO;

namespace SharpCompress.Common.Rar.Headers
{
    internal class EndArchiveHeader : RarHeader
    {
        public EndArchiveHeader(RarHeader header, RarCrcBinaryReader reader) 
            : base(header, reader, HeaderType.EndArchive) {
        }

        protected override void ReadFromReader(MarkingBinaryReader reader)
        {
            if (this.IsRar5) 
            {
                EndArchiveFlags = reader.ReadRarVIntUInt16();
            }
            else
            {
                EndArchiveFlags = HeaderFlags;
                if (HasEndArchiveFlag(EndArchiveFlagsV4.DataCrc))
                {
                    ArchiveCrc = reader.ReadInt32();
                }
                if (HasEndArchiveFlag(EndArchiveFlagsV4.VolumeNumber))
                {
                    VolumeNumber = reader.ReadInt16();
                }
            }
        }

        private ushort EndArchiveFlags { get; set; }

        private bool HasEndArchiveFlag(ushort flag) 
        {
            return (EndArchiveFlags & flag) == flag;
        }

        internal int? ArchiveCrc { get; private set; }

        internal short? VolumeNumber { get; private set; }
    }
}