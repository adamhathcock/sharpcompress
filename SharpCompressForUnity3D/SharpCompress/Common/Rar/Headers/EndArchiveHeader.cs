namespace SharpCompress.Common.Rar.Headers
{
    using SharpCompress.IO;
    using System;
    using System.Runtime.CompilerServices;

    internal class EndArchiveHeader : RarHeader
    {
        [CompilerGenerated]
        private int? <ArchiveCRC>k__BackingField;
        [CompilerGenerated]
        private short? <VolumeNumber>k__BackingField;

        private bool EndArchiveFlags_HasFlag(SharpCompress.Common.Rar.Headers.EndArchiveFlags endArchiveFlags)
        {
            return ((this.EndArchiveFlags & endArchiveFlags) == endArchiveFlags);
        }

        protected override void ReadFromReader(MarkingBinaryReader reader)
        {
            if (this.EndArchiveFlags_HasFlag(SharpCompress.Common.Rar.Headers.EndArchiveFlags.EARC_DATACRC))
            {
                this.ArchiveCRC = new int?(reader.ReadInt32());
            }
            if (this.EndArchiveFlags_HasFlag(SharpCompress.Common.Rar.Headers.EndArchiveFlags.EARC_VOLNUMBER))
            {
                this.VolumeNumber = new short?(reader.ReadInt16());
            }
        }

        internal int? ArchiveCRC
        {
            [CompilerGenerated]
            get
            {
                return this.<ArchiveCRC>k__BackingField;
            }
            [CompilerGenerated]
            private set
            {
                this.<ArchiveCRC>k__BackingField = value;
            }
        }

        internal SharpCompress.Common.Rar.Headers.EndArchiveFlags EndArchiveFlags
        {
            get
            {
                return (SharpCompress.Common.Rar.Headers.EndArchiveFlags) base.Flags;
            }
        }

        internal short? VolumeNumber
        {
            [CompilerGenerated]
            get
            {
                return this.<VolumeNumber>k__BackingField;
            }
            [CompilerGenerated]
            private set
            {
                this.<VolumeNumber>k__BackingField = value;
            }
        }
    }
}

