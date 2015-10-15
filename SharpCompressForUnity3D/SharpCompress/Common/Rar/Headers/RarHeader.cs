namespace SharpCompress.Common.Rar.Headers
{
    using SharpCompress.Common;
    using SharpCompress.IO;
    using System;
    using System.IO;
    using System.Runtime.CompilerServices;

    internal class RarHeader
    {
        [CompilerGenerated]
        private uint <AdditionalSize>k__BackingField;
        [CompilerGenerated]
        private short <Flags>k__BackingField;
        [CompilerGenerated]
        private short <HeadCRC>k__BackingField;
        [CompilerGenerated]
        private short <HeaderSize>k__BackingField;
        [CompilerGenerated]
        private SharpCompress.Common.Rar.Headers.HeaderType <HeaderType>k__BackingField;
        [CompilerGenerated]
        private long <ReadBytes>k__BackingField;
        internal const short BaseBlockSize = 7;
        internal const short LONG_BLOCK = -32768;

        internal static RarHeader Create(MarkingBinaryReader reader)
        {
            try
            {
                RarHeader header = new RarHeader();
                reader.Mark();
                header.ReadFromReader(reader);
                header.ReadBytes += reader.CurrentReadByteCount;
                return header;
            }
            catch (EndOfStreamException)
            {
                return null;
            }
        }

        private void FillBase(RarHeader baseHeader)
        {
            this.HeadCRC = baseHeader.HeadCRC;
            this.HeaderType = baseHeader.HeaderType;
            this.Flags = baseHeader.Flags;
            this.HeaderSize = baseHeader.HeaderSize;
            this.AdditionalSize = baseHeader.AdditionalSize;
            this.ReadBytes = baseHeader.ReadBytes;
        }

        protected virtual void PostReadingBytes(MarkingBinaryReader reader)
        {
        }

        internal T PromoteHeader<T>(MarkingBinaryReader reader) where T: RarHeader, new()
        {
            T local = Activator.CreateInstance<T>();
            local.FillBase(this);
            reader.Mark();
            local.ReadFromReader(reader);
            local.ReadBytes += reader.CurrentReadByteCount;
            int count = local.HeaderSize - ((int) local.ReadBytes);
            if (count > 0)
            {
                reader.ReadBytes(count);
            }
            return local;
        }

        protected virtual void ReadFromReader(MarkingBinaryReader reader)
        {
            this.HeadCRC = reader.ReadInt16();
            this.HeaderType = ((SharpCompress.Common.Rar.Headers.HeaderType) reader.ReadByte()) & ((SharpCompress.Common.Rar.Headers.HeaderType) 0xff);
            this.Flags = reader.ReadInt16();
            this.HeaderSize = reader.ReadInt16();
            if (FlagUtility.HasFlag(this.Flags, -32768))
            {
                this.AdditionalSize = reader.ReadUInt32();
            }
        }

        protected uint AdditionalSize
        {
            [CompilerGenerated]
            get
            {
                return this.<AdditionalSize>k__BackingField;
            }
            [CompilerGenerated]
            private set
            {
                this.<AdditionalSize>k__BackingField = value;
            }
        }

        protected short Flags
        {
            [CompilerGenerated]
            get
            {
                return this.<Flags>k__BackingField;
            }
            [CompilerGenerated]
            private set
            {
                this.<Flags>k__BackingField = value;
            }
        }

        protected short HeadCRC
        {
            [CompilerGenerated]
            get
            {
                return this.<HeadCRC>k__BackingField;
            }
            [CompilerGenerated]
            private set
            {
                this.<HeadCRC>k__BackingField = value;
            }
        }

        protected short HeaderSize
        {
            [CompilerGenerated]
            get
            {
                return this.<HeaderSize>k__BackingField;
            }
            [CompilerGenerated]
            private set
            {
                this.<HeaderSize>k__BackingField = value;
            }
        }

        internal SharpCompress.Common.Rar.Headers.HeaderType HeaderType
        {
            [CompilerGenerated]
            get
            {
                return this.<HeaderType>k__BackingField;
            }
            [CompilerGenerated]
            private set
            {
                this.<HeaderType>k__BackingField = value;
            }
        }

        protected long ReadBytes
        {
            [CompilerGenerated]
            get
            {
                return this.<ReadBytes>k__BackingField;
            }
            [CompilerGenerated]
            private set
            {
                this.<ReadBytes>k__BackingField = value;
            }
        }
    }
}

