#nullable disable

using System;
using System.Buffers;
using System.Buffers.Binary;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace SharpCompress.Common.Tar.Headers
{
    internal sealed class TarHeader
    {
        internal static readonly DateTime EPOCH = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        public TarHeader(ArchiveEncoding archiveEncoding)
        {
            ArchiveEncoding = archiveEncoding;
        }

        internal string Name { get; set; }
        internal string LinkName { get; set; }

        //internal int Mode { get; set; }
        //internal int UserId { get; set; }
        //internal string UserName { get; set; }
        //internal int GroupId { get; set; }
        //internal string GroupName { get; set; }
        internal long Size { get; set; }
        internal DateTime LastModifiedTime { get; set; }
        internal EntryType EntryType { get; set; }
        internal Stream PackedStream { get; set; }
        internal ArchiveEncoding ArchiveEncoding { get; }

        internal const int BLOCK_SIZE = 512;

        internal async Task WriteAsync(Stream output)
        {
            using var buffer = MemoryPool<byte>.Shared.Rent(BLOCK_SIZE);

            WriteOctalBytes(511, buffer.Memory.Span, 100, 8); // file mode
            WriteOctalBytes(0, buffer.Memory.Span, 108, 8); // owner ID
            WriteOctalBytes(0, buffer.Memory.Span, 116, 8); // group ID

            //ArchiveEncoding.UTF8.GetBytes("magic").CopyTo(buffer, 257);
            var nameByteCount = ArchiveEncoding.GetEncoding().GetByteCount(Name);
            if (nameByteCount > 100)
            {
                // Set mock filename and filetype to indicate the next block is the actual name of the file
                WriteStringBytes("././@LongLink", buffer.Memory.Span, 0, 100);
                buffer.Memory.Span[156] = (byte)EntryType.LongName;
                WriteOctalBytes(nameByteCount + 1, buffer.Memory.Span, 124, 12);
            }
            else
            {
                WriteStringBytes(ArchiveEncoding.Encode(Name), buffer.Memory, 100);
                WriteOctalBytes(Size, buffer.Memory.Span, 124, 12);
                var time = (long)(LastModifiedTime.ToUniversalTime() - EPOCH).TotalSeconds;
                WriteOctalBytes(time, buffer.Memory.Span, 136, 12);
                buffer.Memory.Span[156] = (byte)EntryType;

                if (Size >= 0x1FFFFFFFF)
                {
                    using var bytes12 = MemoryPool<byte>.Shared.Rent(12);
                    BinaryPrimitives.WriteInt64BigEndian(bytes12.Memory.Span.Slice(4), Size);
                    bytes12.Memory.Span[0] |= 0x80;
                    bytes12.Memory.CopyTo(buffer.Memory.Slice(124));
                }
            }

            int crc = RecalculateChecksum(buffer.Memory);
            WriteOctalBytes(crc, buffer.Memory.Span, 148, 8);

            await output.WriteAsync(buffer.Memory.Slice(0, BLOCK_SIZE));

            if (nameByteCount > 100)
            {
                await WriteLongFilenameHeaderAsync(output);
                // update to short name lower than 100 - [max bytes of one character].
                // subtracting bytes is needed because preventing infinite loop(example code is here).
                //
                // var bytes = Encoding.UTF8.GetBytes(new string(0x3042, 100));
                // var truncated = Encoding.UTF8.GetBytes(Encoding.UTF8.GetString(bytes, 0, 100));
                //
                // and then infinite recursion is occured in WriteLongFilenameHeader because truncated.Length is 102.
                Name = ArchiveEncoding.Decode(ArchiveEncoding.Encode(Name), 0, 100 - ArchiveEncoding.GetEncoding().GetMaxByteCount(1));
                await WriteAsync(output);
            }
        }

        private async Task WriteLongFilenameHeaderAsync(Stream output)
        {
            byte[] nameBytes = ArchiveEncoding.Encode(Name);
            await output.WriteAsync(nameBytes.AsMemory());

            // pad to multiple of BlockSize bytes, and make sure a terminating null is added
            int numPaddingBytes = BLOCK_SIZE - (nameBytes.Length % BLOCK_SIZE);
            if (numPaddingBytes == 0)
            {
                numPaddingBytes = BLOCK_SIZE;
            }

            using var padding = MemoryPool<byte>.Shared.Rent(numPaddingBytes);
            padding.Memory.Span.Clear();
            await output.WriteAsync(padding.Memory.Slice(0, numPaddingBytes));
        }

        internal async ValueTask<bool> Read(Stream stream, CancellationToken cancellationToken)
        {
            var block = MemoryPool<byte>.Shared.Rent(BLOCK_SIZE);
            bool readFullyAsync = await stream.ReadAsync(block.Memory.Slice(0, BLOCK_SIZE), cancellationToken) == BLOCK_SIZE;
            if (readFullyAsync is false)
            {
                return false;
            }

            // for symlinks, additionally read the linkname
            if (ReadEntryType(block.Memory.Span) == EntryType.SymLink)
            {
                LinkName = ArchiveEncoding.Decode(block.Memory.Span.Slice(157, 100)).TrimNulls();
            }

            if (ReadEntryType(block.Memory.Span) == EntryType.LongName)
            {
                Name = await ReadLongName(stream, block.Memory.Slice(0,BLOCK_SIZE), cancellationToken);
                readFullyAsync = await stream.ReadAsync(block.Memory.Slice(0, BLOCK_SIZE), cancellationToken) == BLOCK_SIZE;
                if (readFullyAsync is false)
                {
                    return false;
                }
            }
            else
            {
                Name = ArchiveEncoding.Decode(block.Memory.Span.Slice( 0, 100)).TrimNulls();
            }

            EntryType = ReadEntryType(block.Memory.Span);
            Size = ReadSize(block.Memory.Slice(0, BLOCK_SIZE));

            //Mode = ReadASCIIInt32Base8(buffer, 100, 7);
            //UserId = ReadASCIIInt32Base8(buffer, 108, 7);
            //GroupId = ReadASCIIInt32Base8(buffer, 116, 7);
            long unixTimeStamp = ReadAsciiInt64Base8(block.Memory.Span.Slice(136, 11));
            LastModifiedTime = EPOCH.AddSeconds(unixTimeStamp).ToLocalTime();

            Magic = ArchiveEncoding.Decode(block.Memory.Span.Slice( 257, 6)).TrimNulls();

            if (!string.IsNullOrEmpty(Magic)
                && "ustar".Equals(Magic))
            {
                string namePrefix = ArchiveEncoding.Decode(block.Memory.Span.Slice( 345, 157));
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

        private async ValueTask<string> ReadLongName(Stream reader, ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken)
        {
            var size = ReadSize(buffer);
            var nameLength = (int)size;
            using var rented = MemoryPool<byte>.Shared.Rent(nameLength);
            var nameBytes = rented.Memory.Slice(0, nameLength);
            await reader.ReadAsync(nameBytes, cancellationToken);
            var remainingBytesToRead = BLOCK_SIZE - (nameLength % BLOCK_SIZE);

            // Read the rest of the block and discard the data
            if (remainingBytesToRead < BLOCK_SIZE)
            {
                using var remaining = MemoryPool<byte>.Shared.Rent(remainingBytesToRead);
                await reader.ReadAsync(remaining.Memory.Slice(0, remainingBytesToRead), cancellationToken);
            }
            return ArchiveEncoding.Decode(nameBytes.Span).TrimNulls();
        }

        private static EntryType ReadEntryType(Span<byte> buffer)
        {
            return (EntryType)buffer[156];
        }

        private long ReadSize(ReadOnlyMemory<byte> buffer)
        {
            if ((buffer.Span[124] & 0x80) == 0x80) // if size in binary
            {
                return BinaryPrimitives.ReadInt64BigEndian(buffer.Span.Slice(0x80));
            }

            return ReadAsciiInt64Base8(buffer.Span.Slice(124, 11));
        }
        private static void WriteStringBytes(ReadOnlySpan<byte> name, Memory<byte> buffer, int length)
        {
            name.CopyTo(buffer.Span.Slice(0));
            int i = Math.Min(length, name.Length);
            buffer.Slice(i, length - i).Span.Clear();
        }

        private static void WriteStringBytes(string name, Span<byte> buffer, int offset, int length)
        {
            int i;

            for (i = 0; i < length && i < name.Length; ++i)
            {
                buffer[offset + i] = (byte)name[i];
            }

            for (; i < length; ++i)
            {
                buffer[offset + i] = 0;
            }
        }

        private static void WriteOctalBytes(long value, Span<byte> buffer, int offset, int length)
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

        private static long ReadAsciiInt64Base8(ReadOnlySpan<byte> buffer)
        {
            string s = Encoding.UTF8.GetString(buffer).TrimNulls();
            if (string.IsNullOrEmpty(s))
            {
                return 0;
            }
            return Convert.ToInt64(s, 8);
        }

        private static long ReadAsciiInt64(byte[] buffer, int offset, int count)
        {
            string s = Encoding.UTF8.GetString(buffer, offset, count).TrimNulls();
            if (string.IsNullOrEmpty(s))
            {
                return 0;
            }
            return Convert.ToInt64(s);
        }


        private static readonly byte[] eightSpaces = {
            (byte)' ', (byte)' ', (byte)' ', (byte)' ',
            (byte)' ', (byte)' ', (byte)' ', (byte)' '
        };

        private static int RecalculateChecksum(Memory<byte> buf)
        {
            // Set default value for checksum. That is 8 spaces.
            eightSpaces.CopyTo(buf.Slice(148));

            // Calculate checksum
            int headerChecksum = 0;
            foreach (byte b in buf.Span)
            {
                headerChecksum += b;
            }
            return headerChecksum;
        }

        public long? DataStartPosition { get; set; }

        public string Magic { get; set; }
    }
}
