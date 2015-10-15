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
        private uint _CompressedSize_k__BackingField;
        [CompilerGenerated]
        private ZipCompressionMethod _CompressionMethod_k__BackingField;
        [CompilerGenerated]
        private uint _Crc_k__BackingField;
        [CompilerGenerated]
        private long? _DataStartPosition_k__BackingField;
        [CompilerGenerated]
        private List<ExtraData> _Extra_k__BackingField;
        [CompilerGenerated]
        private HeaderFlags _Flags_k__BackingField;
        [CompilerGenerated]
        private ushort _LastModifiedDate_k__BackingField;
        [CompilerGenerated]
        private ushort _LastModifiedTime_k__BackingField;
        [CompilerGenerated]
        private string _Name_k__BackingField;
        [CompilerGenerated]
        private Stream _PackedStream_k__BackingField;
        [CompilerGenerated]
        private ZipFilePart _Part_k__BackingField;
        [CompilerGenerated]
        private SharpCompress.Common.Zip.PkwareTraditionalEncryptionData _PkwareTraditionalEncryptionData_k__BackingField;
        [CompilerGenerated]
        private uint _UncompressedSize_k__BackingField;

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
                return this._CompressedSize_k__BackingField;
            }
            [CompilerGenerated]
            set
            {
                this._CompressedSize_k__BackingField = value;
            }
        }

        internal ZipCompressionMethod CompressionMethod
        {
            [CompilerGenerated]
            get
            {
                return this._CompressionMethod_k__BackingField;
            }
            [CompilerGenerated]
            set
            {
                this._CompressionMethod_k__BackingField = value;
            }
        }

        internal uint Crc
        {
            [CompilerGenerated]
            get
            {
                return this._Crc_k__BackingField;
            }
            [CompilerGenerated]
            set
            {
                this._Crc_k__BackingField = value;
            }
        }

        internal long? DataStartPosition
        {
            [CompilerGenerated]
            get
            {
                return this._DataStartPosition_k__BackingField;
            }
            [CompilerGenerated]
            set
            {
                this._DataStartPosition_k__BackingField = value;
            }
        }

        internal List<ExtraData> Extra
        {
            [CompilerGenerated]
            get
            {
                return this._Extra_k__BackingField;
            }
            [CompilerGenerated]
            set
            {
                this._Extra_k__BackingField = value;
            }
        }

        internal HeaderFlags Flags
        {
            [CompilerGenerated]
            get
            {
                return this._Flags_k__BackingField;
            }
            [CompilerGenerated]
            set
            {
                this._Flags_k__BackingField = value;
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
                return this._LastModifiedDate_k__BackingField;
            }
            [CompilerGenerated]
            set
            {
                this._LastModifiedDate_k__BackingField = value;
            }
        }

        internal ushort LastModifiedTime
        {
            [CompilerGenerated]
            get
            {
                return this._LastModifiedTime_k__BackingField;
            }
            [CompilerGenerated]
            set
            {
                this._LastModifiedTime_k__BackingField = value;
            }
        }

        internal string Name
        {
            [CompilerGenerated]
            get
            {
                return this._Name_k__BackingField;
            }
            [CompilerGenerated]
            set
            {
                this._Name_k__BackingField = value;
            }
        }

        internal Stream PackedStream
        {
            [CompilerGenerated]
            get
            {
                return this._PackedStream_k__BackingField;
            }
            [CompilerGenerated]
            set
            {
                this._PackedStream_k__BackingField = value;
            }
        }

        internal ZipFilePart Part
        {
            [CompilerGenerated]
            get
            {
                return this._Part_k__BackingField;
            }
            [CompilerGenerated]
            set
            {
                this._Part_k__BackingField = value;
            }
        }

        internal SharpCompress.Common.Zip.PkwareTraditionalEncryptionData PkwareTraditionalEncryptionData
        {
            [CompilerGenerated]
            get
            {
                return this._PkwareTraditionalEncryptionData_k__BackingField;
            }
            [CompilerGenerated]
            set
            {
                this._PkwareTraditionalEncryptionData_k__BackingField = value;
            }
        }

        internal uint UncompressedSize
        {
            [CompilerGenerated]
            get
            {
                return this._UncompressedSize_k__BackingField;
            }
            [CompilerGenerated]
            set
            {
                this._UncompressedSize_k__BackingField = value;
            }
        }
    }
}

