using System;
using System.Buffers.Binary;
using System.IO;

namespace SharpCompress.Common.Dmg.Headers
{
    internal sealed class GptHeader : GptStructBase
    {
        private const int HeaderSize = 92;
        private static readonly ulong Signature = BinaryPrimitives.ReadUInt64LittleEndian(new byte[] { 69, 70, 73, 32, 80, 65, 82, 84 });

        public uint Revision { get; }
        public uint Crc32Header { get; }
        public ulong CurrentLba { get; }
        public ulong BackupLba { get; }
        public ulong FirstUsableLba { get; }
        public ulong LastUsableLba { get; }
        public Guid DiskGuid { get; }
        public ulong EntriesStart { get; }
        public uint EntriesCount { get; }
        public uint EntriesSize { get; }
        public uint Crc32Array { get; }

        private GptHeader(
            uint revision,
            uint crc32Header,
            ulong currentLba,
            ulong backupLba,
            ulong firstUsableLba,
            ulong lastUsableLba,
            Guid diskGuid,
            ulong entriesStart,
            uint entriesCount,
            uint entriesSize,
            uint crc32Array)
        {
            Revision = revision;
            Crc32Header = crc32Header;
            CurrentLba = currentLba;
            BackupLba = backupLba;
            FirstUsableLba = firstUsableLba;
            LastUsableLba = lastUsableLba;
            DiskGuid = diskGuid;
            EntriesStart = entriesStart;
            EntriesCount = entriesCount;
            EntriesSize = entriesSize;
            Crc32Array = crc32Array;
        }

        public static bool TryRead(Stream stream, out GptHeader? header)
        {
            header = null;

            ulong sig = ReadUInt64(stream);
            if (sig != Signature) return false;

            uint revision = ReadUInt32(stream);

            uint headerSize = ReadUInt32(stream);
            if (headerSize != HeaderSize) return false;

            uint crc32Header = ReadUInt32(stream);
            _ = ReadUInt32(stream); // reserved
            ulong currentLba = ReadUInt64(stream);
            ulong backupLba = ReadUInt64(stream);
            ulong firstUsableLba = ReadUInt64(stream);
            ulong lastUsableLba = ReadUInt64(stream);
            Guid diskGuid = ReadGuid(stream);
            ulong entriesStart = ReadUInt64(stream);
            uint entriesCount = ReadUInt32(stream);
            uint entriesSize = ReadUInt32(stream);
            uint crc32Array = ReadUInt32(stream);

            header = new GptHeader(
                revision,
                crc32Header,
                currentLba,
                backupLba,
                firstUsableLba,
                lastUsableLba,
                diskGuid,
                entriesStart,
                entriesCount,
                entriesSize,
                crc32Array);

            return true;
        }
    }
}
