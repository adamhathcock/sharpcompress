namespace SharpCompress
{
    internal class WrappedEntry : IEntry
    {
        private readonly Common.IEntry entry;

        internal WrappedEntry(Common.IEntry entry)
        {
            this.entry = entry;
        }

        public CompressionType CompressionType
        {
            get { return (CompressionType)entry.CompressionType; }
        }

        public System.DateTimeOffset ArchivedTime
        {
            get { throw new System.NotImplementedException(); }
        }

        public long CompressedSize
        {
            get { return entry.CompressedSize; }
        }

        public uint Crc
        {
            get { return entry.Crc; }
        }

        public System.DateTimeOffset CreatedTime
        {
            get { throw new System.NotImplementedException(); }
        }

        public string FilePath
        {
            get { return entry.FilePath; }
        }

        public bool IsDirectory
        {
            get { return entry.IsDirectory; }
        }

        public bool IsEncrypted
        {
            get { return entry.IsEncrypted; }
        }

        public bool IsSplit
        {
            get { return entry.IsSplit; }
        }

        public System.DateTimeOffset LastAccessedTime
        {
            get { throw new System.NotImplementedException(); }
        }

        public System.DateTimeOffset LastModifiedTime
        {
            get { throw new System.NotImplementedException(); }
        }

        public long Size
        {
            get { return entry.Size; }
        }
    }
}