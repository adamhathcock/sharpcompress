using System;
using System.IO;
#if !PORTABLE
using System.Net;
#endif
using System.Text;

namespace SharpCompress.Common.Tar.Headers
{
    internal enum EntryType : byte
    {
        File = 0,
        OldFile = (byte) '0',
        HardLink = (byte) '1',
        SymLink = (byte) '2',
        CharDevice = (byte) '3',
        BlockDevice = (byte) '4',
        Directory = (byte) '5',
        Fifo = (byte) '6',
        LongLink = (byte) 'K',
        LongName = (byte) 'L',
        SparseFile = (byte) 'S',
        VolumeHeader = (byte) 'V',
    }

    internal class TarHeader
    {
        internal static readonly DateTime Epoch = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        internal string Name { get; set; }
        //internal int Mode { get; set; }
        //internal int UserId { get; set; }
        //internal string UserName { get; set; }
        //internal int GroupId { get; set; }
        //internal string GroupName { get; set; }
        internal long Size { get; set; }
        internal DateTime LastModifiedTime { get; set; }
        internal EntryType EntryType { get; set; }
        internal Stream PackedStream { get; set; }

        internal void Write(Stream output)
        {
            byte[] buffer = new byte[512];

            WriteOctalBytes(511, buffer, 100, 8);   // file mode
            WriteOctalBytes(0, buffer, 108, 8);     // owner ID
            WriteOctalBytes(0, buffer, 116, 8);     // group ID

            //Encoding.UTF8.GetBytes("magic").CopyTo(buffer, 257);
            if (Name.Length > 100)
            {
                // Set mock filename and filetype to indicate the next block is the actual name of the file
                WriteStringBytes("././@LongLink", buffer, 0, 100);
                buffer[156] = (byte)EntryType.LongName;
                WriteOctalBytes(Name.Length + 1, buffer, 124, 12);
            }
            else
            {
                WriteStringBytes(Name, buffer, 0, 100);
                WriteOctalBytes(Size, buffer, 124, 12);
                var time = (long)(LastModifiedTime.ToUniversalTime() - Epoch).TotalSeconds;
                WriteOctalBytes(time, buffer, 136, 12);
                buffer[156] = (byte)EntryType;

                if (Size >= 0x1FFFFFFFF)
                {
#if PORTABLE || NETFX_CORE
                byte[] bytes = BitConverter.GetBytes(Utility.HostToNetworkOrder(Size));
#else
                    byte[] bytes = BitConverter.GetBytes(IPAddress.HostToNetworkOrder(Size));
#endif
                    var bytes12 = new byte[12];
                    bytes.CopyTo(bytes12, 12 - bytes.Length);
                    bytes12[0] |= 0x80;
                    bytes12.CopyTo(buffer, 124);
                }
            }

            int crc = RecalculateChecksum(buffer);
            WriteOctalBytes(crc, buffer, 148, 8);

            output.Write(buffer, 0, buffer.Length);

            if (Name.Length > 100)
            {
                WriteLongFilenameHeader(output);
                Name = Name.Substring(0, 100);
                Write(output);
            }
        }
        private void WriteLongFilenameHeader(Stream output)
        {
            byte[] nameBytes = ArchiveEncoding.Default.GetBytes(Name);
            output.Write(nameBytes, 0, nameBytes.Length);

            // pad to multiple of 512 bytes, and make sure a terminating null is added
            int numPaddingBytes = 512 - (nameBytes.Length % 512);
            if (numPaddingBytes == 0)
                numPaddingBytes = 512;
            output.Write(new byte[numPaddingBytes], 0, numPaddingBytes);
        }

        internal bool Read(BinaryReader reader)
        {
            byte[] buffer = reader.ReadBytes(512);
            if (buffer.Length == 0)
            {
                return false;
            }
            if (buffer.Length < 512)
            {
                throw new InvalidOperationException();
            }
            Name = ArchiveEncoding.Default.GetString(buffer, 0, 100).TrimNulls();

            //Mode = ReadASCIIInt32Base8(buffer, 100, 7);
            //UserId = ReadASCIIInt32Base8(buffer, 108, 7);
            //GroupId = ReadASCIIInt32Base8(buffer, 116, 7);
            EntryType = (EntryType) buffer[156];
            if ((buffer[124] & 0x80) == 0x80) // if size in binary
            {
                long sizeBigEndian = BitConverter.ToInt64(buffer, 0x80);
#if PORTABLE || NETFX_CORE
                    Size = Utility.NetworkToHostOrder(sizeBigEndian);
#else
                Size = IPAddress.NetworkToHostOrder(sizeBigEndian);
#endif
            }
            else
            {
                Size = ReadASCIIInt64Base8(buffer, 124, 11);
            }
            long unixTimeStamp = ReadASCIIInt64Base8(buffer, 136, 11);
            LastModifiedTime = Epoch.AddSeconds(unixTimeStamp).ToLocalTime();


            Magic = ArchiveEncoding.Default.GetString(buffer, 257, 6).TrimNulls();

            if (!string.IsNullOrEmpty(Magic) && "ustar".Equals(Magic))
            {
                string namePrefix = ArchiveEncoding.Default.GetString(buffer, 345, 157);
                namePrefix = namePrefix.TrimNulls();
                if (!string.IsNullOrEmpty(namePrefix))
                {
                    Name = namePrefix + "/" + Name;
                }
            }
            if (EntryType != EntryType.LongName && Name.Length == 0)
            {
                return false;
            }
            return true;
        }

        private static void WriteStringBytes(string name, byte[] buffer, int offset, int length)
        {
            int i;

            for (i = 0; i < length - 1 && i < name.Length; ++i)
            {
                buffer[offset + i] = (byte) name[i];
            }

            for (; i < length; ++i)
            {
                buffer[offset + i] = 0;
            }
        }

        private static void WriteOctalBytes(long value, byte[] buffer, int offset, int length)
        {
            string val = Convert.ToString(value, 8);
            int shift = length - val.Length - 1;
            for (int i = 0; i < shift; i++)
            {
                buffer[offset + i] = (byte) ' ';
            }
            for (int i = 0; i < val.Length; i++)
            {
                buffer[offset + i + shift] = (byte) val[i];
            }
        }

        private static int ReadASCIIInt32Base8(byte[] buffer, int offset, int count)
        {
            string s = Encoding.UTF8.GetString(buffer, offset, count).TrimNulls();
            if (string.IsNullOrEmpty(s))
            {
                return 0;
            }
            return Convert.ToInt32(s, 8);
        }

        private static long ReadASCIIInt64Base8(byte[] buffer, int offset, int count)
        {
            string s = Encoding.UTF8.GetString(buffer, offset, count).TrimNulls();
            if (string.IsNullOrEmpty(s))
            {
                return 0;
            }
            return Convert.ToInt64(s, 8);
        }

        private static long ReadASCIIInt64(byte[] buffer, int offset, int count)
        {
            string s = Encoding.UTF8.GetString(buffer, offset, count).TrimNulls();
            if (string.IsNullOrEmpty(s))
            {
                return 0;
            }
            return Convert.ToInt64(s);
        }

        internal static int RecalculateChecksum(byte[] buf)
        {
            // Set default value for checksum. That is 8 spaces.
            Encoding.UTF8.GetBytes("        ").CopyTo(buf, 148);

            // Calculate checksum
            int headerChecksum = 0;
            foreach (byte b in buf)
            {
                headerChecksum += b;
            }
            return headerChecksum;
        }

        internal static int RecalculateAltChecksum(byte[] buf)
        {
            Encoding.UTF8.GetBytes("        ").CopyTo(buf, 148);
            int headerChecksum = 0;
            foreach (byte b in buf)
            {
                if ((b & 0x80) == 0x80)
                {
                    headerChecksum -= b ^ 0x80;
                }
                else
                {
                    headerChecksum += b;
                }
            }
            return headerChecksum;
        }

        public long? DataStartPosition { get; set; }

        public string Magic { get; set; }
    }
}