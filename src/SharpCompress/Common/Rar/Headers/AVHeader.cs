using SharpCompress.IO;

namespace SharpCompress.Common.Rar.Headers
{
    internal class AVHeader : RarHeader
    {
        public AVHeader(RarHeader header, RarCrcBinaryReader reader) 
            : base(header, reader, HeaderType.Av) 
        {
            if (IsRar5) 
                throw new InvalidFormatException("unexpected rar5 record");
        }

        protected override void ReadFinish(MarkingBinaryReader reader)
        {
            UnpackVersion = reader.ReadByte();
            Method = reader.ReadByte();
            AvVersion = reader.ReadByte();
            AvInfoCrc = reader.ReadInt32();
        }

        internal int AvInfoCrc { get; private set; }

        internal byte UnpackVersion { get; private set; }

        internal byte Method { get; private set; }

        internal byte AvVersion { get; private set; }
    }
}