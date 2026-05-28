using SharpCompress.Common.Rar;
using SharpCompress.IO;

namespace SharpCompress.Common.Rar.Headers;

internal sealed partial class ArchiveCryptHeader : RarHeader
{
    public static ArchiveCryptHeader Create(RarHeader header, RarCrcBinaryReader reader) =>
        CreateChild<ArchiveCryptHeader>(header, reader, HeaderType.Crypt);

    public Rar5CryptoInfo CryptInfo = default!;

    protected sealed override void ReadFinish(MarkingBinaryReader reader) =>
        CryptInfo = Rar5CryptoInfo.Create(reader, false);
}
