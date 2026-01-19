using SharpCompress.IO;

namespace SharpCompress.Common.Rar.Headers;

internal class CommentHeader : RarHeader
{
    public static CommentHeader Create(RarHeader header, RarCrcBinaryReader reader)
    {
        var c = CreateChild<CommentHeader>(header, reader, HeaderType.Comment);
        if (c.IsRar5)
        {
            throw new InvalidFormatException("unexpected rar5 record");
        }
        return c;
    }

    protected override void ReadFinish(MarkingBinaryReader reader)
    {
        UnpSize = reader.ReadInt16();
        UnpVersion = reader.ReadByte();
        UnpMethod = reader.ReadByte();
        CommCrc = reader.ReadInt16();
    }

    internal short UnpSize { get; private set; }

    internal byte UnpVersion { get; private set; }

    internal byte UnpMethod { get; private set; }
    internal short CommCrc { get; private set; }
}
