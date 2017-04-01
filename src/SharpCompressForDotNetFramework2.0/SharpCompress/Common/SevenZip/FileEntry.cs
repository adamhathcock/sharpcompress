
namespace SharpCompress.Common.SevenZip
{
    internal class HeaderEntry
    {
        public bool IsAnti;
        public bool HasStream;
        public bool IsDirectory;
        public ulong Size;
        public uint? FileCRC;
        public string Name;
        public Folder Folder;
        public UnpackedStreamInfo UnpackedStream;
        public ulong FolderOffset;
    }
}
