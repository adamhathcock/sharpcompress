using SharpCompress.IO;

namespace SharpCompress.Common.Rar.Headers
{
    internal class CommentHeader : RarHeader
    {
        protected CommentHeader(RarHeader header, RarCrcBinaryReader reader)
            : base(header, reader, HeaderType.Comment) 
        { 
            if (IsRar5) throw new InvalidFormatException("unexpected rar5 record");
        }

        protected override void ReadFinish(MarkingBinaryReader reader)
        {
            UnpSize = reader.ReadInt16();
            UnpVersion = reader.ReadByte();
            UnpMethod = reader.ReadByte();
            CommCRC = reader.ReadInt16();
        }

        internal short UnpSize { get; private set; }

        internal byte UnpVersion { get; private set; }

        internal byte UnpMethod { get; private set; }
        internal short CommCRC { get; private set; }
    }
}