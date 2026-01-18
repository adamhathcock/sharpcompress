#nullable disable

using System.Threading;
using System.Threading.Tasks;
using SharpCompress.Common.Rar;
using SharpCompress.IO;

namespace SharpCompress.Common.Rar.Headers;

internal class ArchiveCryptHeader : RarHeader
{
    public ArchiveCryptHeader(RarHeader header, RarCrcBinaryReader reader)
        : base(header, reader, HeaderType.Crypt) { }

    public Rar5CryptoInfo CryptInfo = new();

    protected override void ReadFinish(MarkingBinaryReader reader) =>
        CryptInfo = new Rar5CryptoInfo(reader, false);

    protected override async ValueTask ReadFinishAsync(
        AsyncMarkingBinaryReader reader,
        CancellationToken cancellationToken = default
    )
    {
        CryptInfo = await Rar5CryptoInfo
            .CreateAsync(reader, false, cancellationToken)
            .ConfigureAwait(false);
    }
}
