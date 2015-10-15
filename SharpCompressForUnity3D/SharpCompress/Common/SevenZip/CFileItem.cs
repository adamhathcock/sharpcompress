namespace SharpCompress.Common.SevenZip
{
    using System;
    using System.Runtime.CompilerServices;

    internal class CFileItem
    {
        [CompilerGenerated]
        private DateTime? <ATime>k__BackingField;
        [CompilerGenerated]
        private uint? <Attrib>k__BackingField;
        [CompilerGenerated]
        private uint? <Crc>k__BackingField;
        [CompilerGenerated]
        private DateTime? <CTime>k__BackingField;
        [CompilerGenerated]
        private bool <HasStream>k__BackingField;
        [CompilerGenerated]
        private bool <IsAnti>k__BackingField;
        [CompilerGenerated]
        private bool <IsDir>k__BackingField;
        [CompilerGenerated]
        private DateTime? <MTime>k__BackingField;
        [CompilerGenerated]
        private string <Name>k__BackingField;
        [CompilerGenerated]
        private long <Size>k__BackingField;
        [CompilerGenerated]
        private long? <StartPos>k__BackingField;

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
                return this.<ATime>k__BackingField;
            }
            [CompilerGenerated]
            internal set
            {
                this.<ATime>k__BackingField = value;
            }
        }

        public uint? Attrib
        {
            [CompilerGenerated]
            get
            {
                return this.<Attrib>k__BackingField;
            }
            [CompilerGenerated]
            internal set
            {
                this.<Attrib>k__BackingField = value;
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
                return this.<Crc>k__BackingField;
            }
            [CompilerGenerated]
            internal set
            {
                this.<Crc>k__BackingField = value;
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
                return this.<CTime>k__BackingField;
            }
            [CompilerGenerated]
            internal set
            {
                this.<CTime>k__BackingField = value;
            }
        }

        public bool HasStream
        {
            [CompilerGenerated]
            get
            {
                return this.<HasStream>k__BackingField;
            }
            [CompilerGenerated]
            internal set
            {
                this.<HasStream>k__BackingField = value;
            }
        }

        public bool IsAnti
        {
            [CompilerGenerated]
            get
            {
                return this.<IsAnti>k__BackingField;
            }
            [CompilerGenerated]
            internal set
            {
                this.<IsAnti>k__BackingField = value;
            }
        }

        public bool IsDir
        {
            [CompilerGenerated]
            get
            {
                return this.<IsDir>k__BackingField;
            }
            [CompilerGenerated]
            internal set
            {
                this.<IsDir>k__BackingField = value;
            }
        }

        public DateTime? MTime
        {
            [CompilerGenerated]
            get
            {
                return this.<MTime>k__BackingField;
            }
            [CompilerGenerated]
            internal set
            {
                this.<MTime>k__BackingField = value;
            }
        }

        public string Name
        {
            [CompilerGenerated]
            get
            {
                return this.<Name>k__BackingField;
            }
            [CompilerGenerated]
            internal set
            {
                this.<Name>k__BackingField = value;
            }
        }

        public long Size
        {
            [CompilerGenerated]
            get
            {
                return this.<Size>k__BackingField;
            }
            [CompilerGenerated]
            internal set
            {
                this.<Size>k__BackingField = value;
            }
        }

        public long? StartPos
        {
            [CompilerGenerated]
            get
            {
                return this.<StartPos>k__BackingField;
            }
            [CompilerGenerated]
            internal set
            {
                this.<StartPos>k__BackingField = value;
            }
        }
    }
}

