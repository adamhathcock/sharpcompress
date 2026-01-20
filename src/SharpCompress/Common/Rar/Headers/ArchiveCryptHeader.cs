#nullable disable

using System.Threading;
using System.Threading.Tasks;
using SharpCompress.Common.Rar;
using SharpCompress.IO;

namespace SharpCompress.Common.Rar.Headers;

internal class ArchiveCryptHeader : RarHeader
{
    public static ArchiveCryptHeader Create(RarHeader header, RarCrcBinaryReader reader) =>
        CreateChild<ArchiveCryptHeader>(header, reader, HeaderType.Crypt);

    public static async ValueTask<ArchiveCryptHeader> CreateAsync(
        RarHeader header,
        AsyncRarCrcBinaryReader reader,
        CancellationToken cancellationToken = default
    ) =>
        await CreateChildAsync<ArchiveCryptHeader>(
                header,
                reader,
                HeaderType.Crypt,
                cancellationToken
            )
            .ConfigureAwait(false);

    public Rar5CryptoInfo CryptInfo = default!;

    protected override void ReadFinish(MarkingBinaryReader reader) =>
        CryptInfo = Rar5CryptoInfo.Create(reader, false);

    protected override async ValueTask ReadFinishAsync(
        AsyncMarkingBinaryReader reader,
        CancellationToken cancellationToken
    )
    {
        CryptInfo = await Rar5CryptoInfo.CreateAsync(reader, false);
    }
}
