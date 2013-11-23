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
        internal static readonly DateTime Epoch = new DateTime(1970, 1, 1, 0, 0, 0);

        internal TarHeader(EntryType entryType)
        {
            EntryType = entryType;
        }

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
        internal static bool IsPathSeparator(char ch)
        {
            return (ch == '\\' || ch == '/' || ch == '|'); // All the path separators I ever met.
        }
        internal void Write(Stream output)
        {
            if (Name.Length > 255)
            {
                throw new InvalidFormatException("UsTar fileName can not be longer thatn 255 chars");
            }
            byte[] buffer = new byte[512];
            string name = Name;
            string namePrefix = null;
            if (name.Length > 100)
            {
                int position = Name.Length - 100;

                // Find first path separator in the remaining 100 chars of the file name
                while (!IsPathSeparator(Name[position]))
                {
                    ++position;
                    if (position == Name.Length)
                    {
                        break;
                    }
                }
                if (position == Name.Length)
                {
                    position = Name.Length - 100;
                }
                namePrefix = Name.Substring(0, position);
                name = Name.Substring(position, Name.Length - position);
            }

            Encoding.ASCII.GetBytes(name.PadRight(100, '\0')).CopyTo(buffer, 0);
            WriteOctalBytes(511, buffer, 100, 8);
            WriteOctalBytes(0, buffer, 108, 8);
            WriteOctalBytes(0, buffer, 116, 8);
            WriteOctalBytes(Size, buffer, 124, 12);
            var time = (long) (LastModifiedTime - Epoch).TotalSeconds;
            WriteOctalBytes(time, buffer, 136, 12);


            if (namePrefix != null)
            {
                Encoding.ASCII.GetBytes(namePrefix).CopyTo(buffer, 347);
                Encoding.ASCII.GetBytes("ustar").CopyTo(buffer, 0x101);
                Encoding.ASCII.GetBytes(" ").CopyTo(buffer, 0x106);
            }
            else
            {
                buffer[156] = (byte)EntryType;
            }
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
            int crc = RecalculateChecksum(buffer);
            WriteOctalBytes(crc, buffer, 148, 8);
            output.Write(buffer, 0, buffer.Length);
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
            Name = Encoding.ASCII.GetString(buffer, 0, 100).TrimNulls();

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
            LastModifiedTime = Epoch.AddSeconds(unixTimeStamp);


            Magic = Encoding.ASCII.GetString(buffer, 257, 5).TrimNulls();

            if (!string.IsNullOrEmpty(Magic) && "ustar".Equals(Magic))
            {
                string namePrefix = ArchiveEncoding.Default.GetString(buffer, 345, 157);
                namePrefix = namePrefix.TrimNulls();
                if (!string.IsNullOrEmpty(namePrefix))
                {
                    Name = namePrefix + Name;
                }
            }
            return true;
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
            buffer[offset + length] = 0;
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