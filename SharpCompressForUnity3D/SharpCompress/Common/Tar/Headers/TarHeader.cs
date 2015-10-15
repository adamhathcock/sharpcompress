namespace SharpCompress.Common.Tar.Headers
{
    using SharpCompress;
    using SharpCompress.Common;
    using System;
    using System.IO;
    using System.Runtime.CompilerServices;
    using System.Text;

    internal class TarHeader
    {
        [CompilerGenerated]
        private long? <DataStartPosition>k__BackingField;
        [CompilerGenerated]
        private SharpCompress.Common.Tar.Headers.EntryType <EntryType>k__BackingField;
        [CompilerGenerated]
        private DateTime <LastModifiedTime>k__BackingField;
        [CompilerGenerated]
        private string <Magic>k__BackingField;
        [CompilerGenerated]
        private string <Name>k__BackingField;
        [CompilerGenerated]
        private Stream <PackedStream>k__BackingField;
        [CompilerGenerated]
        private long <Size>k__BackingField;
        internal static readonly DateTime Epoch = new DateTime(0x7b2, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        internal bool Read(BinaryReader reader)
        {
            byte[] bytes = reader.ReadBytes(0x200);
            if (bytes.Length == 0)
            {
                return false;
            }
            if (bytes.Length < 0x200)
            {
                throw new InvalidOperationException();
            }
            this.Name = Utility.TrimNulls(ArchiveEncoding.Default.GetString(bytes, 0, 100));
            this.EntryType = (SharpCompress.Common.Tar.Headers.EntryType) bytes[0x9c];
            if ((bytes[0x7c] & 0x80) == 0x80)
            {
                long network = BitConverter.ToInt64(bytes, 0x80);
                this.Size = Utility.NetworkToHostOrder(network);
            }
            else
            {
                this.Size = ReadASCIIInt64Base8(bytes, 0x7c, 11);
            }
            long num2 = ReadASCIIInt64Base8(bytes, 0x88, 11);
            this.LastModifiedTime = Epoch.AddSeconds((double) num2).ToLocalTime();
            this.Magic = Utility.TrimNulls(ArchiveEncoding.Default.GetString(bytes, 0x101, 6));
            if (!string.IsNullOrEmpty(this.Magic) && "ustar".Equals(this.Magic))
            {
                string str = Utility.TrimNulls(ArchiveEncoding.Default.GetString(bytes, 0x159, 0x9d));
                if (!string.IsNullOrEmpty(str))
                {
                    this.Name = str + "/" + this.Name;
                }
            }
            if ((this.EntryType != SharpCompress.Common.Tar.Headers.EntryType.LongName) && (this.Name.Length == 0))
            {
                return false;
            }
            return true;
        }

        private static int ReadASCIIInt32Base8(byte[] buffer, int offset, int count)
        {
            string str = Utility.TrimNulls(Encoding.UTF8.GetString(buffer, offset, count));
            if (string.IsNullOrEmpty(str))
            {
                return 0;
            }
            return Convert.ToInt32(str, 8);
        }

        private static long ReadASCIIInt64(byte[] buffer, int offset, int count)
        {
            string str = Utility.TrimNulls(Encoding.UTF8.GetString(buffer, offset, count));
            if (string.IsNullOrEmpty(str))
            {
                return 0L;
            }
            return Convert.ToInt64(str);
        }

        private static long ReadASCIIInt64Base8(byte[] buffer, int offset, int count)
        {
            string str = Utility.TrimNulls(Encoding.UTF8.GetString(buffer, offset, count));
            if (string.IsNullOrEmpty(str))
            {
                return 0L;
            }
            return Convert.ToInt64(str, 8);
        }

        internal static int RecalculateAltChecksum(byte[] buf)
        {
            Encoding.UTF8.GetBytes("        ").CopyTo(buf, 0x94);
            int num = 0;
            foreach (byte num2 in buf)
            {
                if ((num2 & 0x80) == 0x80)
                {
                    num -= num2 ^ 0x80;
                }
                else
                {
                    num += num2;
                }
            }
            return num;
        }

        internal static int RecalculateChecksum(byte[] buf)
        {
            Encoding.UTF8.GetBytes("        ").CopyTo(buf, 0x94);
            int num = 0;
            foreach (byte num2 in buf)
            {
                num += num2;
            }
            return num;
        }

        internal void Write(Stream output)
        {
            byte[] buffer = new byte[0x200];
            WriteOctalBytes(0x1ffL, buffer, 100, 8);
            WriteOctalBytes(0L, buffer, 0x6c, 8);
            WriteOctalBytes(0L, buffer, 0x74, 8);
            if (this.Name.Length > 100)
            {
                WriteStringBytes("././@LongLink", buffer, 0, 100);
                buffer[0x9c] = 0x4c;
                WriteOctalBytes((long) (this.Name.Length + 1), buffer, 0x7c, 12);
            }
            else
            {
                WriteStringBytes(this.Name, buffer, 0, 100);
                WriteOctalBytes(this.Size, buffer, 0x7c, 12);
                TimeSpan span = (TimeSpan) (this.LastModifiedTime.ToUniversalTime() - Epoch);
                long totalSeconds = (long) span.TotalSeconds;
                WriteOctalBytes(totalSeconds, buffer, 0x88, 12);
                buffer[0x9c] = (byte) this.EntryType;
                if (this.Size >= 0x1ffffffffL)
                {
                    byte[] bytes = BitConverter.GetBytes(Utility.HostToNetworkOrder(this.Size));
                    byte[] array = new byte[12];
                    bytes.CopyTo(array, (int) (12 - bytes.Length));
                    array[0] = (byte) (array[0] | 0x80);
                    array.CopyTo(buffer, 0x7c);
                }
            }
            WriteOctalBytes((long) RecalculateChecksum(buffer), buffer, 0x94, 8);
            output.Write(buffer, 0, buffer.Length);
            if (this.Name.Length > 100)
            {
                this.WriteLongFilenameHeader(output);
                this.Name = this.Name.Substring(0, 100);
                this.Write(output);
            }
        }

        private void WriteLongFilenameHeader(Stream output)
        {
            byte[] bytes = ArchiveEncoding.Default.GetBytes(this.Name);
            output.Write(bytes, 0, bytes.Length);
            int count = 0x200 - (bytes.Length % 0x200);
            if (count == 0)
            {
                count = 0x200;
            }
            output.Write(new byte[count], 0, count);
        }

        private static void WriteOctalBytes(long value, byte[] buffer, int offset, int length)
        {
            int num2;
            string str = Convert.ToString(value, 8);
            int num = (length - str.Length) - 1;
            for (num2 = 0; num2 < num; num2++)
            {
                buffer[offset + num2] = 0x20;
            }
            for (num2 = 0; num2 < str.Length; num2++)
            {
                buffer[(offset + num2) + num] = (byte) str[num2];
            }
        }

        private static void WriteStringBytes(string name, byte[] buffer, int offset, int length)
        {
            int num = 0;
            while ((num < (length - 1)) && (num < name.Length))
            {
                buffer[offset + num] = (byte) name[num];
                num++;
            }
            while (num < length)
            {
                buffer[offset + num] = 0;
                num++;
            }
        }

        public long? DataStartPosition
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

        internal SharpCompress.Common.Tar.Headers.EntryType EntryType
        {
            [CompilerGenerated]
            get
            {
                return this.<EntryType>k__BackingField;
            }
            [CompilerGenerated]
            set
            {
                this.<EntryType>k__BackingField = value;
            }
        }

        internal DateTime LastModifiedTime
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

        public string Magic
        {
            [CompilerGenerated]
            get
            {
                return this.<Magic>k__BackingField;
            }
            [CompilerGenerated]
            set
            {
                this.<Magic>k__BackingField = value;
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

        internal long Size
        {
            [CompilerGenerated]
            get
            {
                return this.<Size>k__BackingField;
            }
            [CompilerGenerated]
            set
            {
                this.<Size>k__BackingField = value;
            }
        }
    }
}

