using System;

namespace SharpCompress.Common.Dmg.HFS
{
    internal sealed class HFSPermissions : HFSStructBase
    {
        public uint OwnerID { get; }
        public uint GroupID { get; }
        public byte AdminFlags { get; }
        public byte OwnerFlags { get; }
        public ushort FileMode { get; }
        public uint Special { get; }

        private HFSPermissions(uint ownerID, uint groupID, byte adminFlags, byte ownerFlags, ushort fileMode, uint special)
        {
            OwnerID = ownerID;
            GroupID = groupID;
            AdminFlags = adminFlags;
            OwnerFlags = ownerFlags;
            FileMode = fileMode;
            Special = special;
        }

        public static HFSPermissions Read(ref ReadOnlySpan<byte> data)
        {
            return new HFSPermissions(
                ReadUInt32(ref data),
                ReadUInt32(ref data),
                ReadUInt8(ref data),
                ReadUInt8(ref data),
                ReadUInt16(ref data),
                ReadUInt32(ref data));
        }
    }
}
