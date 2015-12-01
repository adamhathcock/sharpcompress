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
        private uint _AdditionalSize_k__BackingField;
        [CompilerGenerated]
        private short _Flags_k__BackingField;
        [CompilerGenerated]
        private short _HeadCRC_k__BackingField;
        [CompilerGenerated]
        private short _HeaderSize_k__BackingField;
        [CompilerGenerated]
        private SharpCompress.Common.Rar.Headers.HeaderType _HeaderType_k__BackingField;
        [CompilerGenerated]
        private long _ReadBytes_k__BackingField;
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
            if (FlagUtility.HasFlag((short)this.Flags, (short)-32768))
            {
                this.AdditionalSize = reader.ReadUInt32();
            }
        }

        protected uint AdditionalSize
        {
            [CompilerGenerated]
            get
            {
                return this._AdditionalSize_k__BackingField;
            }
            [CompilerGenerated]
            private set
            {
                this._AdditionalSize_k__BackingField = value;
            }
        }

        protected short Flags
        {
            [CompilerGenerated]
            get
            {
                return this._Flags_k__BackingField;
            }
            [CompilerGenerated]
            private set
            {
                this._Flags_k__BackingField = value;
            }
        }

        protected short HeadCRC
        {
            [CompilerGenerated]
            get
            {
                return this._HeadCRC_k__BackingField;
            }
            [CompilerGenerated]
            private set
            {
                this._HeadCRC_k__BackingField = value;
            }
        }

        protected short HeaderSize
        {
            [CompilerGenerated]
            get
            {
                return this._HeaderSize_k__BackingField;
            }
            [CompilerGenerated]
            private set
            {
                this._HeaderSize_k__BackingField = value;
            }
        }

        internal SharpCompress.Common.Rar.Headers.HeaderType HeaderType
        {
            [CompilerGenerated]
            get
            {
                return this._HeaderType_k__BackingField;
            }
            [CompilerGenerated]
            private set
            {
                this._HeaderType_k__BackingField = value;
            }
        }

        protected long ReadBytes
        {
            [CompilerGenerated]
            get
            {
                return this._ReadBytes_k__BackingField;
            }
            [CompilerGenerated]
            private set
            {
                this._ReadBytes_k__BackingField = value;
            }
        }
    }
}

