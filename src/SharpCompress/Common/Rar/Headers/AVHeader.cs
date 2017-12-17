using SharpCompress.IO;

namespace SharpCompress.Common.Rar.Headers
{
    internal class AVHeader : RarHeader
    {
        public AVHeader(RarHeader header, RarCrcBinaryReader reader) 
            : base(header, reader, HeaderType.Av) {
            if (IsRar5) throw new InvalidFormatException("unexpected rar5 record");
        }

        protected override void ReadFromReader(MarkingBinaryReader reader)
        {
            UnpackVersion = reader.ReadByte();
            Method = reader.ReadByte();
            AVVersion = reader.ReadByte();
            AVInfoCRC = reader.ReadInt32();
        }

        internal int AVInfoCRC { get; private set; }

        internal byte UnpackVersion { get; private set; }

        internal byte Method { get; private set; }

        internal byte AVVersion { get; private set; }
    }
}