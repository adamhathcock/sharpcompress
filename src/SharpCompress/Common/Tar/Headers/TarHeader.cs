using System;
using System.IO;
using System.Text;
using SharpCompress.Converter;

namespace SharpCompress.Common.Tar.Headers
{
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

        internal const int BlockSize = 512;

        internal void Write(Stream output)
        {
            byte[] buffer = new byte[BlockSize];

            WriteOctalBytes(511, buffer, 100, 8); // file mode
            WriteOctalBytes(0, buffer, 108, 8); // owner ID
            WriteOctalBytes(0, buffer, 116, 8); // group ID

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
                    byte[] bytes = DataConverter.BigEndian.GetBytes(Size);
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

            // pad to multiple of BlockSize bytes, and make sure a terminating null is added
            int numPaddingBytes = BlockSize - (nameBytes.Length % BlockSize);
            if (numPaddingBytes == 0)
                numPaddingBytes = BlockSize;
            output.Write(new byte[numPaddingBytes], 0, numPaddingBytes);
        }

        internal bool Read(BinaryReader reader)
        {
            var buffer = ReadBlock(reader);
            if (buffer.Length == 0)
            {
                return false;
            }

            if (ReadEntryType(buffer) == EntryType.LongName)
            {
                Name = ReadLongName(reader, buffer);
                buffer = ReadBlock(reader);
            }
            else
            {
                Name = ArchiveEncoding.Default.GetString(buffer, 0, 100).TrimNulls();
            }

            EntryType = ReadEntryType(buffer);
            Size = ReadSize(buffer);

            //Mode = ReadASCIIInt32Base8(buffer, 100, 7);
            //UserId = ReadASCIIInt32Base8(buffer, 108, 7);
            //GroupId = ReadASCIIInt32Base8(buffer, 116, 7);
            long unixTimeStamp = ReadASCIIInt64Base8(buffer, 136, 11);
            LastModifiedTime = Epoch.AddSeconds(unixTimeStamp).ToLocalTime();

            Magic = ArchiveEncoding.Default.GetString(buffer, 257, 6).TrimNulls();

            if (!string.IsNullOrEmpty(Magic)
                && "ustar".Equals(Magic))
            {
                string namePrefix = ArchiveEncoding.Default.GetString(buffer, 345, 157);
                namePrefix = namePrefix.TrimNulls();
                if (!string.IsNullOrEmpty(namePrefix))
                {
                    Name = namePrefix + "/" + Name;
                }
            }
            if (EntryType != EntryType.LongName
                && Name.Length == 0)
            {
                return false;
            }
            return true;
        }

        private string ReadLongName(BinaryReader reader, byte[] buffer)
        {
            var size = ReadSize(buffer);
            var nameLength = (int) size;
            var nameBytes = reader.ReadBytes(nameLength);
            var remainingBytesToRead = BlockSize - (nameLength%BlockSize);
            // Read the rest of the block and discard the data
            if (remainingBytesToRead < BlockSize) reader.ReadBytes(remainingBytesToRead);
            return ArchiveEncoding.Default.GetString(nameBytes, 0, nameBytes.Length).TrimNulls();
        }

        private static EntryType ReadEntryType(byte[] buffer)
        {
            return (EntryType)buffer[156];
        }

        private long ReadSize(byte[] buffer)
        {
            if ((buffer[124] & 0x80) == 0x80) // if size in binary
            {
                return DataConverter.BigEndian.GetInt64(buffer, 0x80);
            }
            return ReadASCIIInt64Base8(buffer, 124, 11);
        }

        private static byte[] ReadBlock(BinaryReader reader)
        {
            byte[] buffer = reader.ReadBytes(BlockSize);

            if (buffer.Length < BlockSize)
            {
                throw new InvalidOperationException();
            }
            return buffer;
        }

        private static void WriteStringBytes(string name, byte[] buffer, int offset, int length)
        {
            int i;

            for (i = 0; i < length - 1 && i < name.Length; ++i)
            {
                buffer[offset + i] = (byte)name[i];
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
                buffer[offset + i] = (byte)' ';
            }
            for (int i = 0; i < val.Length; i++)
            {
                buffer[offset + i + shift] = (byte)val[i];
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