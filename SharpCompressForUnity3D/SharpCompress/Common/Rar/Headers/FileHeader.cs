namespace SharpCompress.Common.Rar.Headers
{
    using SharpCompress;
    using SharpCompress.Common;
    using SharpCompress.IO;
    using System;
    using System.IO;
    using System.Runtime.CompilerServices;

    internal class FileHeader : RarHeader
    {
        [CompilerGenerated]
        private long <CompressedSize>k__BackingField;
        [CompilerGenerated]
        private long <DataStartPosition>k__BackingField;
        [CompilerGenerated]
        private DateTime? <FileArchivedTime>k__BackingField;
        [CompilerGenerated]
        private int <FileAttributes>k__BackingField;
        [CompilerGenerated]
        private uint <FileCRC>k__BackingField;
        [CompilerGenerated]
        private DateTime? <FileCreatedTime>k__BackingField;
        [CompilerGenerated]
        private DateTime? <FileLastAccessedTime>k__BackingField;
        [CompilerGenerated]
        private DateTime? <FileLastModifiedTime>k__BackingField;
        [CompilerGenerated]
        private string <FileName>k__BackingField;
        [CompilerGenerated]
        private SharpCompress.Common.Rar.Headers.HostOS <HostOS>k__BackingField;
        [CompilerGenerated]
        private Stream <PackedStream>k__BackingField;
        [CompilerGenerated]
        private byte <PackingMethod>k__BackingField;
        [CompilerGenerated]
        private byte <RarVersion>k__BackingField;
        [CompilerGenerated]
        private int <RecoverySectors>k__BackingField;
        [CompilerGenerated]
        private byte[] <Salt>k__BackingField;
        [CompilerGenerated]
        private byte[] <SubData>k__BackingField;
        [CompilerGenerated]
        private long <UncompressedSize>k__BackingField;
        private const byte NEWLHD_SIZE = 0x20;
        private const byte SALT_SIZE = 8;

        private static string ConvertPath(string path, SharpCompress.Common.Rar.Headers.HostOS os)
        {
            return path.Replace('\\', '/');
        }

        private string DecodeDefault(byte[] bytes)
        {
            return ArchiveEncoding.Default.GetString(bytes, 0, bytes.Length);
        }

        private bool FileFlags_HasFlag(SharpCompress.Common.Rar.Headers.FileFlags fileFlags)
        {
            return (((ushort) (this.FileFlags & fileFlags)) == fileFlags);
        }

        private static DateTime? ProcessExtendedTime(ushort extendedFlags, DateTime? time, MarkingBinaryReader reader, int i)
        {
            uint num = (uint) (extendedFlags >> ((3 - i) * 4));
            if ((num & 8) == 0)
            {
                return null;
            }
            if (i != 0)
            {
                uint iTime = reader.ReadUInt32();
                time = new DateTime?(Utility.DosDateToDateTime(iTime));
            }
            if ((num & 4) == 0)
            {
                time = new DateTime?(time.Value.AddSeconds(1.0));
            }
            uint num3 = 0;
            int num4 = ((int) num) & 3;
            for (int j = 0; j < num4; j++)
            {
                byte num6 = reader.ReadByte();
                num3 |= (uint) (num6 << (((j + 3) - num4) * 8));
            }
            return new DateTime?(time.Value.AddMilliseconds(num3 * Math.Pow(10.0, -4.0)));
        }

        protected override void ReadFromReader(MarkingBinaryReader reader)
        {
            uint y = reader.ReadUInt32();
            this.HostOS = (SharpCompress.Common.Rar.Headers.HostOS) reader.ReadByte();
            this.FileCRC = reader.ReadUInt32();
            this.FileLastModifiedTime = new DateTime?(Utility.DosDateToDateTime(reader.ReadInt32()));
            this.RarVersion = reader.ReadByte();
            this.PackingMethod = reader.ReadByte();
            short count = reader.ReadInt16();
            this.FileAttributes = reader.ReadInt32();
            uint x = 0;
            uint num4 = 0;
            if (this.FileFlags_HasFlag(SharpCompress.Common.Rar.Headers.FileFlags.LARGE))
            {
                x = reader.ReadUInt32();
                num4 = reader.ReadUInt32();
            }
            else if (y == uint.MaxValue)
            {
                y = uint.MaxValue;
                num4 = 0x7fffffff;
            }
            this.CompressedSize = this.UInt32To64(x, base.AdditionalSize);
            this.UncompressedSize = this.UInt32To64(num4, y);
            count = (count > 0x1000) ? ((short) 0x1000) : count;
            byte[] name = reader.ReadBytes(count);
            switch (base.HeaderType)
            {
                case HeaderType.FileHeader:
                    if (this.FileFlags_HasFlag(SharpCompress.Common.Rar.Headers.FileFlags.UNICODE))
                    {
                        int index = 0;
                        while ((index < name.Length) && (name[index] != 0))
                        {
                            index++;
                        }
                        if (index != count)
                        {
                            index++;
                            this.FileName = FileNameDecoder.Decode(name, index);
                        }
                        else
                        {
                            this.FileName = this.DecodeDefault(name);
                        }
                    }
                    else
                    {
                        this.FileName = this.DecodeDefault(name);
                    }
                    this.FileName = ConvertPath(this.FileName, this.HostOS);
                    break;

                case HeaderType.NewSubHeader:
                {
                    int num6 = (base.HeaderSize - 0x20) - count;
                    if (this.FileFlags_HasFlag(SharpCompress.Common.Rar.Headers.FileFlags.SALT))
                    {
                        num6 -= 8;
                    }
                    if (num6 > 0)
                    {
                        this.SubData = reader.ReadBytes(num6);
                    }
                    if (NewSubHeaderType.SUBHEAD_TYPE_RR.Equals(name))
                    {
                        this.RecoverySectors = ((this.SubData[8] + (this.SubData[9] << 8)) + (this.SubData[10] << 0x10)) + (this.SubData[11] << 0x18);
                    }
                    break;
                }
            }
            if (this.FileFlags_HasFlag(SharpCompress.Common.Rar.Headers.FileFlags.SALT))
            {
                this.Salt = reader.ReadBytes(8);
            }
            if (this.FileFlags_HasFlag(SharpCompress.Common.Rar.Headers.FileFlags.EXTTIME) && ((base.ReadBytes + reader.CurrentReadByteCount) <= (base.HeaderSize - 2)))
            {
                ushort extendedFlags = reader.ReadUInt16();
                this.FileLastModifiedTime = ProcessExtendedTime(extendedFlags, this.FileLastModifiedTime, reader, 0);
                DateTime? time = null;
                this.FileCreatedTime = ProcessExtendedTime(extendedFlags, time, reader, 1);
                time = null;
                this.FileLastAccessedTime = ProcessExtendedTime(extendedFlags, time, reader, 2);
                this.FileArchivedTime = ProcessExtendedTime(extendedFlags, null, reader, 3);
            }
        }

        public override string ToString()
        {
            return this.FileName;
        }

        private long UInt32To64(uint x, uint y)
        {
            long num = x;
            num = num << 0x20;
            return (num + y);
        }

        internal long CompressedSize
        {
            [CompilerGenerated]
            get
            {
                return this.<CompressedSize>k__BackingField;
            }
            [CompilerGenerated]
            private set
            {
                this.<CompressedSize>k__BackingField = value;
            }
        }

        internal long DataStartPosition
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

        internal DateTime? FileArchivedTime
        {
            [CompilerGenerated]
            get
            {
                return this.<FileArchivedTime>k__BackingField;
            }
            [CompilerGenerated]
            private set
            {
                this.<FileArchivedTime>k__BackingField = value;
            }
        }

        internal int FileAttributes
        {
            [CompilerGenerated]
            get
            {
                return this.<FileAttributes>k__BackingField;
            }
            [CompilerGenerated]
            private set
            {
                this.<FileAttributes>k__BackingField = value;
            }
        }

        internal uint FileCRC
        {
            [CompilerGenerated]
            get
            {
                return this.<FileCRC>k__BackingField;
            }
            [CompilerGenerated]
            private set
            {
                this.<FileCRC>k__BackingField = value;
            }
        }

        internal DateTime? FileCreatedTime
        {
            [CompilerGenerated]
            get
            {
                return this.<FileCreatedTime>k__BackingField;
            }
            [CompilerGenerated]
            private set
            {
                this.<FileCreatedTime>k__BackingField = value;
            }
        }

        internal SharpCompress.Common.Rar.Headers.FileFlags FileFlags
        {
            get
            {
                return (SharpCompress.Common.Rar.Headers.FileFlags) ((ushort) base.Flags);
            }
        }

        internal DateTime? FileLastAccessedTime
        {
            [CompilerGenerated]
            get
            {
                return this.<FileLastAccessedTime>k__BackingField;
            }
            [CompilerGenerated]
            private set
            {
                this.<FileLastAccessedTime>k__BackingField = value;
            }
        }

        internal DateTime? FileLastModifiedTime
        {
            [CompilerGenerated]
            get
            {
                return this.<FileLastModifiedTime>k__BackingField;
            }
            [CompilerGenerated]
            private set
            {
                this.<FileLastModifiedTime>k__BackingField = value;
            }
        }

        internal string FileName
        {
            [CompilerGenerated]
            get
            {
                return this.<FileName>k__BackingField;
            }
            [CompilerGenerated]
            private set
            {
                this.<FileName>k__BackingField = value;
            }
        }

        internal SharpCompress.Common.Rar.Headers.HostOS HostOS
        {
            [CompilerGenerated]
            get
            {
                return this.<HostOS>k__BackingField;
            }
            [CompilerGenerated]
            private set
            {
                this.<HostOS>k__BackingField = value;
            }
        }

        public Stream PackedStream
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

        internal byte PackingMethod
        {
            [CompilerGenerated]
            get
            {
                return this.<PackingMethod>k__BackingField;
            }
            [CompilerGenerated]
            private set
            {
                this.<PackingMethod>k__BackingField = value;
            }
        }

        internal byte RarVersion
        {
            [CompilerGenerated]
            get
            {
                return this.<RarVersion>k__BackingField;
            }
            [CompilerGenerated]
            private set
            {
                this.<RarVersion>k__BackingField = value;
            }
        }

        internal int RecoverySectors
        {
            [CompilerGenerated]
            get
            {
                return this.<RecoverySectors>k__BackingField;
            }
            [CompilerGenerated]
            private set
            {
                this.<RecoverySectors>k__BackingField = value;
            }
        }

        internal byte[] Salt
        {
            [CompilerGenerated]
            get
            {
                return this.<Salt>k__BackingField;
            }
            [CompilerGenerated]
            private set
            {
                this.<Salt>k__BackingField = value;
            }
        }

        internal byte[] SubData
        {
            [CompilerGenerated]
            get
            {
                return this.<SubData>k__BackingField;
            }
            [CompilerGenerated]
            private set
            {
                this.<SubData>k__BackingField = value;
            }
        }

        internal long UncompressedSize
        {
            [CompilerGenerated]
            get
            {
                return this.<UncompressedSize>k__BackingField;
            }
            [CompilerGenerated]
            private set
            {
                this.<UncompressedSize>k__BackingField = value;
            }
        }
    }
}

