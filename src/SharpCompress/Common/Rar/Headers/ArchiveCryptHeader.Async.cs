using System.Threading;
using System.Threading.Tasks;
using SharpCompress.Common.Rar;
using SharpCompress.IO;

namespace SharpCompress.Common.Rar.Headers;

internal sealed partial class ArchiveCryptHeader
{
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

    protected sealed override async ValueTask ReadFinishAsync(
        AsyncMarkingBinaryReader reader,
        CancellationToken cancellationToken = default
    )
    {
        CryptInfo = await Rar5CryptoInfo.CreateAsync(reader, false).ConfigureAwait(false);
    }
}
