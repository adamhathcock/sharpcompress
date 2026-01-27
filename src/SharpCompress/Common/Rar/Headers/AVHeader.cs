using SharpCompress.IO;

namespace SharpCompress.Common.Rar.Headers;

internal class AvHeader : RarHeader
{
    public static AvHeader Create(RarHeader header, RarCrcBinaryReader reader)
    {
        var c = CreateChild<AvHeader>(header, reader, HeaderType.Av);
        if (c.IsRar5)
        {
            throw new InvalidFormatException("unexpected rar5 record");
        }
        return c;
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
