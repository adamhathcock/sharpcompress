namespace SharpCompress.Common.Rar.Headers
{
    using SharpCompress.IO;
    using System;
    using System.Runtime.CompilerServices;

    internal class MarkHeader : RarHeader
    {
        [CompilerGenerated]
        private bool <OldFormat>k__BackingField;

        internal bool IsSignature()
        {
            return false;
        }

        internal bool IsValid()
        {
            if (base.HeadCRC != 0x6152)
            {
                return false;
            }
            if (base.HeaderType != HeaderType.MarkHeader)
            {
                return false;
            }
            if (base.Flags != 0x1a21)
            {
                return false;
            }
            if (base.HeaderSize != 7)
            {
                return false;
            }
            return true;
        }

        protected override void ReadFromReader(MarkingBinaryReader reader)
        {
        }

        internal bool OldFormat
        {
            [CompilerGenerated]
            get
            {
                return this.<OldFormat>k__BackingField;
            }
            [CompilerGenerated]
            private set
            {
                this.<OldFormat>k__BackingField = value;
            }
        }
    }
}

