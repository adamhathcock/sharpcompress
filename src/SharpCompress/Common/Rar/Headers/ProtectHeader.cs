using SharpCompress.IO;

namespace SharpCompress.Common.Rar.Headers
{
    // ProtectHeader is part of the Recovery Record feature
    internal class ProtectHeader : RarHeader
    {
        protected override void ReadFromReader(MarkingBinaryReader reader)
        {
            Version = reader.ReadByte();
            RecSectors = reader.ReadUInt16();
            TotalBlocks = reader.ReadUInt32();
            Mark = reader.ReadBytes(8);
        }

        internal uint DataSize { get { return AdditionalSize; } }
        internal byte Version { get; private set; }
        internal ushort RecSectors { get; private set; }
        internal uint TotalBlocks { get; private set; }
        internal byte[] Mark { get; private set; }
    }
}
