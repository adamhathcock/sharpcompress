namespace SharpCompress.Common.Rar.Headers
{
    using SharpCompress;
    using SharpCompress.Common;
    using SharpCompress.IO;
    using System;
    using System.IO;
    using System.Runtime.CompilerServices;

    //internal class FileHeader : RarHeader
    //{
    //    [CompilerGenerated]
    //    private long _CompressedSize_k__BackingField;
    //    [CompilerGenerated]
    //    private long _DataStartPosition_k__BackingField;
    //    [CompilerGenerated]
    //    private DateTime? _FileArchivedTime_k__BackingField;
    //    [CompilerGenerated]
    //    private int _FileAttributes_k__BackingField;
    //    [CompilerGenerated]
    //    private uint _FileCRC_k__BackingField;
    //    [CompilerGenerated]
    //    private DateTime? _FileCreatedTime_k__BackingField;
    //    [CompilerGenerated]
    //    private DateTime? _FileLastAccessedTime_k__BackingField;
    //    [CompilerGenerated]
    //    private DateTime? _FileLastModifiedTime_k__BackingField;
    //    [CompilerGenerated]
    //    private string _FileName_k__BackingField;
    //    [CompilerGenerated]
    //    private SharpCompress.Common.Rar.Headers.HostOS _HostOS_k__BackingField;
    //    [CompilerGenerated]
    //    private Stream _PackedStream_k__BackingField;
    //    [CompilerGenerated]
    //    private byte _PackingMethod_k__BackingField;
    //    [CompilerGenerated]
    //    private byte _RarVersion_k__BackingField;
    //    [CompilerGenerated]
    //    private int _RecoverySectors_k__BackingField;
    //    [CompilerGenerated]
    //    private byte[] _Salt_k__BackingField;
    //    [CompilerGenerated]
    //    private byte[] _SubData_k__BackingField;
    //    [CompilerGenerated]
    //    private long _UncompressedSize_k__BackingField;
    //    private const byte NEWLHD_SIZE = 0x20;
    //    private const byte SALT_SIZE = 8;

    //    private static string ConvertPath(string path, SharpCompress.Common.Rar.Headers.HostOS os)
    //    {
    //        return path.Replace('\\', '/');
    //    }

    //    private string DecodeDefault(byte[] bytes)
    //    {
    //        return ArchiveEncoding.Default.GetString(bytes, 0, bytes.Length);
    //    }

    //    private bool FileFlags_HasFlag(SharpCompress.Common.Rar.Headers.FileFlags fileFlags)
    //    {
    //        return (((ushort) (this.FileFlags & fileFlags)) == fileFlags);
    //    }

    //    private static DateTime? ProcessExtendedTime(ushort extendedFlags, DateTime? time, MarkingBinaryReader reader, int i)
    //    {
    //        uint num = (uint) (extendedFlags >> ((3 - i) * 4));
    //        if ((num & 8) == 0)
    //        {
    //            return null;
    //        }
    //        if (i != 0)
    //        {
    //            uint iTime = reader.ReadUInt32();
    //            time = new DateTime?(Utility.DosDateToDateTime(iTime));
    //        }
    //        if ((num & 4) == 0)
    //        {
    //            time = new DateTime?(time.Value.AddSeconds(1.0));
    //        }
    //        uint num3 = 0;
    //        int num4 = ((int) num) & 3;
    //        for (int j = 0; j < num4; j++)
    //        {
    //            byte num6 = reader.ReadByte();
    //            num3 |= (uint) (num6 << (((j + 3) - num4) * 8));
    //        }
    //        return new DateTime?(time.Value.AddMilliseconds(num3 * Math.Pow(10.0, -4.0)));
    //    }

    //    protected override void ReadFromReader(MarkingBinaryReader reader)
    //    {
    //        uint y = reader.ReadUInt32();
    //        this.HostOS = (SharpCompress.Common.Rar.Headers.HostOS) reader.ReadByte();
    //        this.FileCRC = reader.ReadUInt32();
    //        this.FileLastModifiedTime = new DateTime?(Utility.DosDateToDateTime(reader.ReadInt32()));
    //        this.RarVersion = reader.ReadByte();
    //        this.PackingMethod = reader.ReadByte();
    //        short count = reader.ReadInt16();
    //        this.FileAttributes = reader.ReadInt32();
    //        uint x = 0;
    //        uint num4 = 0;
    //        if (this.FileFlags_HasFlag(SharpCompress.Common.Rar.Headers.FileFlags.LARGE))
    //        {
    //            x = reader.ReadUInt32();
    //            num4 = reader.ReadUInt32();
    //        }
    //        else if (y == uint.MaxValue)
    //        {
    //            y = uint.MaxValue;
    //            num4 = 0x7fffffff;
    //        }
    //        this.CompressedSize = this.UInt32To64(x, base.AdditionalSize);
    //        this.UncompressedSize = this.UInt32To64(num4, y);
    //        count = (count > 0x1000) ? ((short) 0x1000) : count;
    //        byte[] name = reader.ReadBytes(count);
    //        switch (base.HeaderType)
    //        {
    //            case HeaderType.FileHeader:
    //                if (this.FileFlags_HasFlag(SharpCompress.Common.Rar.Headers.FileFlags.UNICODE))
    //                {
    //                    int index = 0;
    //                    while ((index < name.Length) && (name[index] != 0))
    //                    {
    //                        index++;
    //                    }
    //                    if (index != count)
    //                    {
    //                        index++;
    //                        this.FileName = FileNameDecoder.Decode(name, index);
    //                    }
    //                    else
    //                    {
    //                        this.FileName = this.DecodeDefault(name);
    //                    }
    //                }
    //                else
    //                {
    //                    this.FileName = this.DecodeDefault(name);
    //                }
    //                this.FileName = ConvertPath(this.FileName, this.HostOS);
    //                break;

    //            case HeaderType.NewSubHeader:
    //            {
    //                int num6 = (base.HeaderSize - 0x20) - count;
    //                if (this.FileFlags_HasFlag(SharpCompress.Common.Rar.Headers.FileFlags.SALT))
    //                {
    //                    num6 -= 8;
    //                }
    //                if (num6 > 0)
    //                {
    //                    this.SubData = reader.ReadBytes(num6);
    //                }
    //                if (NewSubHeaderType.SUBHEAD_TYPE_RR.Equals(name))
    //                {
    //                    this.RecoverySectors = ((this.SubData[8] + (this.SubData[9] << 8)) + (this.SubData[10] << 0x10)) + (this.SubData[11] << 0x18);
    //                }
    //                break;
    //            }
    //        }
    //        if (this.FileFlags_HasFlag(SharpCompress.Common.Rar.Headers.FileFlags.SALT))
    //        {
    //            this.Salt = reader.ReadBytes(8);
    //        }
    //        if (this.FileFlags_HasFlag(SharpCompress.Common.Rar.Headers.FileFlags.EXTTIME) && ((base.ReadBytes + reader.CurrentReadByteCount) <= (base.HeaderSize - 2)))
    //        {
    //            ushort extendedFlags = reader.ReadUInt16();
    //            this.FileLastModifiedTime = ProcessExtendedTime(extendedFlags, this.FileLastModifiedTime, reader, 0);
    //            DateTime? time = null;
    //            this.FileCreatedTime = ProcessExtendedTime(extendedFlags, time, reader, 1);
    //            time = null;
    //            this.FileLastAccessedTime = ProcessExtendedTime(extendedFlags, time, reader, 2);
    //            this.FileArchivedTime = ProcessExtendedTime(extendedFlags, null, reader, 3);
    //        }
    //    }

    //    public override string ToString()
    //    {
    //        return this.FileName;
    //    }

    //    private long UInt32To64(uint x, uint y)
    //    {
    //        long num = x;
    //        num = num << 0x20;
    //        return (num + y);
    //    }

    //    internal long CompressedSize
    //    {
    //        [CompilerGenerated]
    //        get
    //        {
    //            return this._CompressedSize_k__BackingField;
    //        }
    //        [CompilerGenerated]
    //        private set
    //        {
    //            this._CompressedSize_k__BackingField = value;
    //        }
    //    }

    //    internal long DataStartPosition
    //    {
    //        [CompilerGenerated]
    //        get
    //        {
    //            return this._DataStartPosition_k__BackingField;
    //        }
    //        [CompilerGenerated]
    //        set
    //        {
    //            this._DataStartPosition_k__BackingField = value;
    //        }
    //    }

    //    internal DateTime? FileArchivedTime
    //    {
    //        [CompilerGenerated]
    //        get
    //        {
    //            return this._FileArchivedTime_k__BackingField;
    //        }
    //        [CompilerGenerated]
    //        private set
    //        {
    //            this._FileArchivedTime_k__BackingField = value;
    //        }
    //    }

    //    internal int FileAttributes
    //    {
    //        [CompilerGenerated]
    //        get
    //        {
    //            return this._FileAttributes_k__BackingField;
    //        }
    //        [CompilerGenerated]
    //        private set
    //        {
    //            this._FileAttributes_k__BackingField = value;
    //        }
    //    }

    //    internal uint FileCRC
    //    {
    //        [CompilerGenerated]
    //        get
    //        {
    //            return this._FileCRC_k__BackingField;
    //        }
    //        [CompilerGenerated]
    //        private set
    //        {
    //            this._FileCRC_k__BackingField = value;
    //        }
    //    }

    //    internal DateTime? FileCreatedTime
    //    {
    //        [CompilerGenerated]
    //        get
    //        {
    //            return this._FileCreatedTime_k__BackingField;
    //        }
    //        [CompilerGenerated]
    //        private set
    //        {
    //            this._FileCreatedTime_k__BackingField = value;
    //        }
    //    }

    //    internal SharpCompress.Common.Rar.Headers.FileFlags FileFlags
    //    {
    //        get
    //        {
    //            return (SharpCompress.Common.Rar.Headers.FileFlags) ((ushort) base.Flags);
    //        }
    //    }

    //    internal DateTime? FileLastAccessedTime
    //    {
    //        [CompilerGenerated]
    //        get
    //        {
    //            return this._FileLastAccessedTime_k__BackingField;
    //        }
    //        [CompilerGenerated]
    //        private set
    //        {
    //            this._FileLastAccessedTime_k__BackingField = value;
    //        }
    //    }

    //    internal DateTime? FileLastModifiedTime
    //    {
    //        [CompilerGenerated]
    //        get
    //        {
    //            return this._FileLastModifiedTime_k__BackingField;
    //        }
    //        [CompilerGenerated]
    //        private set
    //        {
    //            this._FileLastModifiedTime_k__BackingField = value;
    //        }
    //    }

    //    internal string FileName
    //    {
    //        [CompilerGenerated]
    //        get
    //        {
    //            return this._FileName_k__BackingField;
    //        }
    //        [CompilerGenerated]
    //        private set
    //        {
    //            this._FileName_k__BackingField = value;
    //        }
    //    }

    //    internal SharpCompress.Common.Rar.Headers.HostOS HostOS
    //    {
    //        [CompilerGenerated]
    //        get
    //        {
    //            return this._HostOS_k__BackingField;
    //        }
    //        [CompilerGenerated]
    //        private set
    //        {
    //            this._HostOS_k__BackingField = value;
    //        }
    //    }

    //    public Stream PackedStream
    //    {
    //        [CompilerGenerated]
    //        get
    //        {
    //            return this._PackedStream_k__BackingField;
    //        }
    //        [CompilerGenerated]
    //        set
    //        {
    //            this._PackedStream_k__BackingField = value;
    //        }
    //    }

    //    internal byte PackingMethod
    //    {
    //        [CompilerGenerated]
    //        get
    //        {
    //            return this._PackingMethod_k__BackingField;
    //        }
    //        [CompilerGenerated]
    //        private set
    //        {
    //            this._PackingMethod_k__BackingField = value;
    //        }
    //    }

    //    internal byte RarVersion
    //    {
    //        [CompilerGenerated]
    //        get
    //        {
    //            return this._RarVersion_k__BackingField;
    //        }
    //        [CompilerGenerated]
    //        private set
    //        {
    //            this._RarVersion_k__BackingField = value;
    //        }
    //    }

    //    internal int RecoverySectors
    //    {
    //        [CompilerGenerated]
    //        get
    //        {
    //            return this._RecoverySectors_k__BackingField;
    //        }
    //        [CompilerGenerated]
    //        private set
    //        {
    //            this._RecoverySectors_k__BackingField = value;
    //        }
    //    }

    //    internal byte[] Salt
    //    {
    //        [CompilerGenerated]
    //        get
    //        {
    //            return this._Salt_k__BackingField;
    //        }
    //        [CompilerGenerated]
    //        private set
    //        {
    //            this._Salt_k__BackingField = value;
    //        }
    //    }

    //    internal byte[] SubData
    //    {
    //        [CompilerGenerated]
    //        get
    //        {
    //            return this._SubData_k__BackingField;
    //        }
    //        [CompilerGenerated]
    //        private set
    //        {
    //            this._SubData_k__BackingField = value;
    //        }
    //    }

    //    internal long UncompressedSize
    //    {
    //        [CompilerGenerated]
    //        get
    //        {
    //            return this._UncompressedSize_k__BackingField;
    //        }
    //        [CompilerGenerated]
    //        private set
    //        {
    //            this._UncompressedSize_k__BackingField = value;
    //        }
    //    }
    //}

    internal class FileHeader : RarHeader {
        private const byte SALT_SIZE = 8;

        private const byte NEWLHD_SIZE = 32;

        protected override void ReadFromReader(MarkingBinaryReader reader) {
            uint lowUncompressedSize = reader.ReadUInt32();

            HostOS = (HostOS)reader.ReadByte();

            FileCRC = reader.ReadUInt32();

            FileLastModifiedTime = Utility.DosDateToDateTime(reader.ReadInt32());

            RarVersion = reader.ReadByte();
            PackingMethod = reader.ReadByte();

            short nameSize = reader.ReadInt16();

            FileAttributes = reader.ReadInt32();

            uint highCompressedSize = 0;
            uint highUncompressedkSize = 0;
            if (FileFlags_HasFlag(FileFlags.LARGE)) {
                highCompressedSize = reader.ReadUInt32();
                highUncompressedkSize = reader.ReadUInt32();
            }
            else {
                if (lowUncompressedSize == 0xffffffff) {
                    lowUncompressedSize = 0xffffffff;
                    highUncompressedkSize = int.MaxValue;
                }
            }
            CompressedSize = UInt32To64(highCompressedSize, AdditionalSize);
            UncompressedSize = UInt32To64(highUncompressedkSize, lowUncompressedSize);

            nameSize = nameSize > 4 * 1024 ? (short)(4 * 1024) : nameSize;

            byte[] fileNameBytes = reader.ReadBytes(nameSize);

            switch (HeaderType) {
                case HeaderType.FileHeader: {
                        if (FileFlags_HasFlag(FileFlags.UNICODE)) {
                            int length = 0;
                            while (length < fileNameBytes.Length
                                   && fileNameBytes[length] != 0) {
                                length++;
                            }
                            if (length != nameSize) {
                                length++;
                                FileName = FileNameDecoder.Decode(fileNameBytes, length);
                            }
                            else {
                                FileName = DecodeDefault(fileNameBytes);
                            }
                        }
                        else {
                            FileName = DecodeDefault(fileNameBytes);
                        }
                        FileName = ConvertPath(FileName, HostOS);
                    }
                    break;
                case HeaderType.NewSubHeader: {
                        int datasize = HeaderSize - NEWLHD_SIZE - nameSize;
                        if (FileFlags_HasFlag(FileFlags.SALT)) {
                            datasize -= SALT_SIZE;
                        }
                        if (datasize > 0) {
                            SubData = reader.ReadBytes(datasize);
                        }

                        if (NewSubHeaderType.SUBHEAD_TYPE_RR.Equals(fileNameBytes)) {
                            RecoverySectors = SubData[8] + (SubData[9] << 8)
                                              + (SubData[10] << 16) + (SubData[11] << 24);
                        }
                    }
                    break;
            }

            if (FileFlags_HasFlag(FileFlags.SALT)) {
                Salt = reader.ReadBytes(SALT_SIZE);
            }
            if (FileFlags_HasFlag(FileFlags.EXTTIME)) {
                // verify that the end of the header hasn't been reached before reading the Extended Time.
                //  some tools incorrectly omit Extended Time despite specifying FileFlags.EXTTIME, which most parsers tolerate.
                if (ReadBytes + reader.CurrentReadByteCount <= HeaderSize - 2) {
                    ushort extendedFlags = reader.ReadUInt16();
                    FileLastModifiedTime = ProcessExtendedTime(extendedFlags, FileLastModifiedTime, reader, 0);
                    FileCreatedTime = ProcessExtendedTime(extendedFlags, null, reader, 1);
                    FileLastAccessedTime = ProcessExtendedTime(extendedFlags, null, reader, 2);
                    FileArchivedTime = ProcessExtendedTime(extendedFlags, null, reader, 3);
                }
            }
        }

        private bool FileFlags_HasFlag(Headers.FileFlags fileFlags) {
            return (FileFlags & fileFlags) == fileFlags;
        }

        //only the full .net framework will do other code pages than unicode/utf8
        private string DecodeDefault(byte[] bytes) {
            return ArchiveEncoding.Default.GetString(bytes, 0, bytes.Length);
        }

        private long UInt32To64(uint x, uint y) {
            long l = x;
            l <<= 32;
            return l + y;
        }

        private static DateTime? ProcessExtendedTime(ushort extendedFlags, DateTime? time, MarkingBinaryReader reader,
                                                     int i) {
            uint rmode = (uint)extendedFlags >> (3 - i) * 4;
            if ((rmode & 8) == 0) {
                return null;
            }
            if (i != 0) {
                uint DosTime = reader.ReadUInt32();
                time = Utility.DosDateToDateTime(DosTime);
            }
            if ((rmode & 4) == 0) {
                time = time.Value.AddSeconds(1);
            }
            uint nanosecondHundreds = 0;
            int count = (int)rmode & 3;
            for (int j = 0; j < count; j++) {
                byte b = reader.ReadByte();
                nanosecondHundreds |= (((uint)b) << ((j + 3 - count) * 8));
            }
            //10^-7 to 10^-3
            return time.Value.AddMilliseconds(nanosecondHundreds * Math.Pow(10, -4));
        }

        private static string ConvertPath(string path, HostOS os) {
#if PORTABLE || NETFX_CORE
            return path.Replace('\\', '/');
#else
            switch (os) {
                case HostOS.MacOS:
                case HostOS.Unix: {
                        if (Path.DirectorySeparatorChar == '\\') {
                            return path.Replace('/', '\\');
                        }
                    }
                    break;
                default: {
                        if (Path.DirectorySeparatorChar == '/') {
                            return path.Replace('\\', '/');
                        }
                    }
                    break;
            }
            return path;
#endif
        }

        internal long DataStartPosition { get; set; }
        internal HostOS HostOS { get; private set; }

        internal uint FileCRC { get; private set; }

        internal DateTime? FileLastModifiedTime { get; private set; }

        internal DateTime? FileCreatedTime { get; private set; }

        internal DateTime? FileLastAccessedTime { get; private set; }

        internal DateTime? FileArchivedTime { get; private set; }

        internal byte RarVersion { get; private set; }

        internal byte PackingMethod { get; private set; }

        internal int FileAttributes { get; private set; }

        internal FileFlags FileFlags {
            get { return (FileFlags)base.Flags; }
        }

        internal long CompressedSize { get; private set; }
        internal long UncompressedSize { get; private set; }

        internal string FileName { get; private set; }

        internal byte[] SubData { get; private set; }

        internal int RecoverySectors { get; private set; }

        internal byte[] Salt { get; private set; }

        public override string ToString() {
            return FileName;
        }

        public Stream PackedStream { get; set; }
    }
}

