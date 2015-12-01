namespace SharpCompress.Common.SevenZip
{
    using System;
    using System.Runtime.CompilerServices;

    internal class CFileItem
    {
        [CompilerGenerated]
        private DateTime? _ATime_k__BackingField;
        [CompilerGenerated]
        private uint? _Attrib_k__BackingField;
        [CompilerGenerated]
        private uint? _Crc_k__BackingField;
        [CompilerGenerated]
        private DateTime? _CTime_k__BackingField;
        [CompilerGenerated]
        private bool _HasStream_k__BackingField;
        [CompilerGenerated]
        private bool _IsAnti_k__BackingField;
        [CompilerGenerated]
        private bool _IsDir_k__BackingField;
        [CompilerGenerated]
        private DateTime? _MTime_k__BackingField;
        [CompilerGenerated]
        private string _Name_k__BackingField;
        [CompilerGenerated]
        private long _Size_k__BackingField;
        [CompilerGenerated]
        private long? _StartPos_k__BackingField;

        internal CFileItem()
        {
            this.HasStream = true;
        }

        public void SetAttrib(uint attrib)
        {
            this.Attrib = new uint?(attrib);
        }

        public DateTime? ATime
        {
            [CompilerGenerated]
            get
            {
                return this._ATime_k__BackingField;
            }
            [CompilerGenerated]
            internal set
            {
                this._ATime_k__BackingField = value;
            }
        }

        public uint? Attrib
        {
            [CompilerGenerated]
            get
            {
                return this._Attrib_k__BackingField;
            }
            [CompilerGenerated]
            internal set
            {
                this._Attrib_k__BackingField = value;
            }
        }

        public bool AttribDefined
        {
            get
            {
                return this.Attrib.HasValue;
            }
        }

        public uint? Crc
        {
            [CompilerGenerated]
            get
            {
                return this._Crc_k__BackingField;
            }
            [CompilerGenerated]
            internal set
            {
                this._Crc_k__BackingField = value;
            }
        }

        public bool CrcDefined
        {
            get
            {
                return this.Crc.HasValue;
            }
        }

        public DateTime? CTime
        {
            [CompilerGenerated]
            get
            {
                return this._CTime_k__BackingField;
            }
            [CompilerGenerated]
            internal set
            {
                this._CTime_k__BackingField = value;
            }
        }

        public bool HasStream
        {
            [CompilerGenerated]
            get
            {
                return this._HasStream_k__BackingField;
            }
            [CompilerGenerated]
            internal set
            {
                this._HasStream_k__BackingField = value;
            }
        }

        public bool IsAnti
        {
            [CompilerGenerated]
            get
            {
                return this._IsAnti_k__BackingField;
            }
            [CompilerGenerated]
            internal set
            {
                this._IsAnti_k__BackingField = value;
            }
        }

        public bool IsDir
        {
            [CompilerGenerated]
            get
            {
                return this._IsDir_k__BackingField;
            }
            [CompilerGenerated]
            internal set
            {
                this._IsDir_k__BackingField = value;
            }
        }

        public DateTime? MTime
        {
            [CompilerGenerated]
            get
            {
                return this._MTime_k__BackingField;
            }
            [CompilerGenerated]
            internal set
            {
                this._MTime_k__BackingField = value;
            }
        }

        public string Name
        {
            [CompilerGenerated]
            get
            {
                return this._Name_k__BackingField;
            }
            [CompilerGenerated]
            internal set
            {
                this._Name_k__BackingField = value;
            }
        }

        public long Size
        {
            [CompilerGenerated]
            get
            {
                return this._Size_k__BackingField;
            }
            [CompilerGenerated]
            internal set
            {
                this._Size_k__BackingField = value;
            }
        }

        public long? StartPos
        {
            [CompilerGenerated]
            get
            {
                return this._StartPos_k__BackingField;
            }
            [CompilerGenerated]
            internal set
            {
                this._StartPos_k__BackingField = value;
            }
        }
    }
}

