using System;
using System.IO;

namespace SharpCompress.Common.Dmg.Headers
{
    internal sealed class GptPartitionEntry : GptStructBase
    {
        public Guid TypeGuid { get; }
        public Guid Guid { get; }
        public ulong FirstLba { get; }
        public ulong LastLba { get; }
        public ulong Attributes { get; }
        public string Name { get; }

        private GptPartitionEntry(Guid typeGuid, Guid guid, ulong firstLba, ulong lastLba, ulong attributes, string name)
        {
            TypeGuid = typeGuid;
            Guid = guid;
            FirstLba = firstLba;
            LastLba = lastLba;
            Attributes = attributes;
            Name = name;
        }

        public static GptPartitionEntry Read(Stream stream)
        {
            return new GptPartitionEntry(
                ReadGuid(stream),
                ReadGuid(stream),
                ReadUInt64(stream),
                ReadUInt64(stream),
                ReadUInt64(stream),
                ReadString(stream, 72));
        }
    }
}
