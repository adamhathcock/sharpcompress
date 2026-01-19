#nullable disable

using SharpCompress.IO;

namespace SharpCompress.Common.Rar.Headers;

internal class ArchiveCryptHeader : RarHeader
{
    public static ArchiveCryptHeader Create(RarHeader header, RarCrcBinaryReader reader) =>
        CreateChild<ArchiveCryptHeader>(header, reader, HeaderType.Crypt);

    public Rar5CryptoInfo CryptInfo = new();

    protected override void ReadFinish(MarkingBinaryReader reader) =>
        CryptInfo = new Rar5CryptoInfo(reader, false);
}
