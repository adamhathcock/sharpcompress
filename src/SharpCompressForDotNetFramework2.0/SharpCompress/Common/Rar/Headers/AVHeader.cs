using SharpCompress.IO;

namespace SharpCompress.Common.Rar.Headers
{
    internal class SignHeader : RarHeader
    {
        protected override void ReadFromReader(MarkingBinaryReader reader)
        {
            CreationTime = reader.ReadInt32();
            ArcNameSize = reader.ReadInt16();
            UserNameSize = reader.ReadInt16();
        }

        internal int CreationTime
        {
            get;
            private set;
        }

        internal short ArcNameSize
        {
            get;
            private set;
        }

        internal short UserNameSize
        {
            get;
            private set;
        }
    }
}
