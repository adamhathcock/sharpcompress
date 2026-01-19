using SharpCompress.IO;

namespace SharpCompress.Common.Rar.Headers;

internal class SignHeader : RarHeader
{
    public static SignHeader Create(RarHeader header, RarCrcBinaryReader reader)
    {
        var c = CreateChild<SignHeader>(header, reader, HeaderType.Sign);
            if (c.IsRar5)
            {
                throw new InvalidFormatException("unexpected rar5 record");
            }
        return c;
    }

    protected override void ReadFinish(MarkingBinaryReader reader)
    {
        CreationTime = reader.ReadInt32();
        ArcNameSize = reader.ReadInt16();
        UserNameSize = reader.ReadInt16();
    }

    internal int CreationTime { get; private set; }

    internal short ArcNameSize { get; private set; }

    internal short UserNameSize { get; private set; }
}
