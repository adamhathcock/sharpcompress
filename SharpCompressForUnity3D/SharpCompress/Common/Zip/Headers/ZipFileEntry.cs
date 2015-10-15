namespace SharpCompress.Common.Zip.Headers
{
    using SharpCompress.Common;
    using SharpCompress.Common.Zip;
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Runtime.CompilerServices;
    using System.Text;

    internal abstract class ZipFileEntry : ZipHeader
    {
        [CompilerGenerated]
        private uint <CompressedSize>k__BackingField;
        [CompilerGenerated]
        private ZipCompressionMethod <CompressionMethod>k__BackingField;
        [CompilerGenerated]
        private uint <Crc>k__BackingField;
        [CompilerGenerated]
        private long? <DataStartPosition>k__BackingField;
        [CompilerGenerated]
        private List<ExtraData> <Extra>k__BackingField;
        [CompilerGenerated]
        private HeaderFlags <Flags>k__BackingField;
        [CompilerGenerated]
        private ushort <LastModifiedDate>k__BackingField;
        [CompilerGenerated]
        private ushort <LastModifiedTime>k__BackingField;
        [CompilerGenerated]
        private string <Name>k__BackingField;
        [CompilerGenerated]
        private Stream <PackedStream>k__BackingField;
        [CompilerGenerated]
        private ZipFilePart <Part>k__BackingField;
        [CompilerGenerated]
        private SharpCompress.Common.Zip.PkwareTraditionalEncryptionData <PkwareTraditionalEncryptionData>k__BackingField;
        [CompilerGenerated]
        private uint <UncompressedSize>k__BackingField;

        protected ZipFileEntry(ZipHeaderType type) : base(type)
        {
            this.Extra = new List<ExtraData>();
        }

        protected string DecodeString(byte[] str)
        {
            if (FlagUtility.HasFlag<HeaderFlags>(this.Flags, HeaderFlags.UTF8))
            {
                return Encoding.UTF8.GetString(str, 0, str.Length);
            }
            return ArchiveEncoding.Default.GetString(str, 0, str.Length);
        }

        protected byte[] EncodeString(string str)
        {
            if (FlagUtility.HasFlag<HeaderFlags>(this.Flags, HeaderFlags.UTF8))
            {
                return Encoding.UTF8.GetBytes(str);
            }
            return ArchiveEncoding.Default.GetBytes(str);
        }

        protected void LoadExtra(byte[] extra)
        {
            ushort num2;
            for (int i = 0; i < (extra.Length - 4); i += num2 + 4)
            {
                ExtraDataType notImplementedExtraData = (ExtraDataType) BitConverter.ToUInt16(extra, i);
                if (!Enum.IsDefined(typeof(ExtraDataType), notImplementedExtraData))
                {
                    notImplementedExtraData = ExtraDataType.NotImplementedExtraData;
                }
                num2 = BitConverter.ToUInt16(extra, i + 2);
                byte[] dst = new byte[num2];
                Buffer.BlockCopy(extra, i + 4, dst, 0, num2);
                this.Extra.Add(LocalEntryHeaderExtraFactory.Create(notImplementedExtraData, num2, dst));
            }
        }

        internal uint CompressedSize
        {
            [CompilerGenerated]
            get
            {
                return this.<CompressedSize>k__BackingField;
            }
            [CompilerGenerated]
            set
            {
                this.<CompressedSize>k__BackingField = value;
            }
        }

        internal ZipCompressionMethod CompressionMethod
        {
            [CompilerGenerated]
            get
            {
                return this.<CompressionMethod>k__BackingField;
            }
            [CompilerGenerated]
            set
            {
                this.<CompressionMethod>k__BackingField = value;
            }
        }

        internal uint Crc
        {
            [CompilerGenerated]
            get
            {
                return this.<Crc>k__BackingField;
            }
            [CompilerGenerated]
            set
            {
                this.<Crc>k__BackingField = value;
            }
        }

        internal long? DataStartPosition
        {
            [CompilerGenerated]
            get
            {
                return this.<DataStartPosition>k__BackingField;
            }
            [CompilerGenerated]
            set
            {
                this.<DataStartPosition>k__BackingField = value;
            }
        }

        internal List<ExtraData> Extra
        {
            [CompilerGenerated]
            get
            {
                return this.<Extra>k__BackingField;
            }
            [CompilerGenerated]
            set
            {
                this.<Extra>k__BackingField = value;
            }
        }

        internal HeaderFlags Flags
        {
            [CompilerGenerated]
            get
            {
                return this.<Flags>k__BackingField;
            }
            [CompilerGenerated]
            set
            {
                this.<Flags>k__BackingField = value;
            }
        }

        internal bool IsDirectory
        {
            get
            {
                return this.Name.EndsWith("/");
            }
        }

        internal ushort LastModifiedDate
        {
            [CompilerGenerated]
            get
            {
                return this.<LastModifiedDate>k__BackingField;
            }
            [CompilerGenerated]
            set
            {
                this.<LastModifiedDate>k__BackingField = value;
            }
        }

        internal ushort LastModifiedTime
        {
            [CompilerGenerated]
            get
            {
                return this.<LastModifiedTime>k__BackingField;
            }
            [CompilerGenerated]
            set
            {
                this.<LastModifiedTime>k__BackingField = value;
            }
        }

        internal string Name
        {
            [CompilerGenerated]
            get
            {
                return this.<Name>k__BackingField;
            }
            [CompilerGenerated]
            set
            {
                this.<Name>k__BackingField = value;
            }
        }

        internal Stream PackedStream
        {
            [CompilerGenerated]
            get
            {
                return this.<PackedStream>k__BackingField;
            }
            [CompilerGenerated]
            set
            {
                this.<PackedStream>k__BackingField = value;
            }
        }

        internal ZipFilePart Part
        {
            [CompilerGenerated]
            get
            {
                return this.<Part>k__BackingField;
            }
            [CompilerGenerated]
            set
            {
                this.<Part>k__BackingField = value;
            }
        }

        internal SharpCompress.Common.Zip.PkwareTraditionalEncryptionData PkwareTraditionalEncryptionData
        {
            [CompilerGenerated]
            get
            {
                return this.<PkwareTraditionalEncryptionData>k__BackingField;
            }
            [CompilerGenerated]
            set
            {
                this.<PkwareTraditionalEncryptionData>k__BackingField = value;
            }
        }

        internal uint UncompressedSize
        {
            [CompilerGenerated]
            get
            {
                return this.<UncompressedSize>k__BackingField;
            }
            [CompilerGenerated]
            set
            {
                this.<UncompressedSize>k__BackingField = value;
            }
        }
    }
}

