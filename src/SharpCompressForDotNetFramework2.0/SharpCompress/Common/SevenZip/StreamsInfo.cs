
using System.Collections.Generic;

namespace SharpCompress.Common.SevenZip
{
    internal class StreamsInfo
    {
        public Folder[] Folders;
        public ulong PackPosition;
        public bool[] EmptyStreamFlags;
        public bool[] EmptyFileFlags;

        public PackedStreamInfo[] PackedStreams;
        public List<UnpackedStreamInfo> UnpackedStreams;
    }

    internal class PackedStreamInfo
    {
        public ulong PackedSize;
        public ulong? Crc;
        public ulong StartPosition;
    }
    internal class UnpackedStreamInfo
    {
        public ulong UnpackedSize;
        public uint? Digest;
    }
}
