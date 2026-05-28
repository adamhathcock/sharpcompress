using System.IO;
using System.Threading;
using System.Threading.Tasks;
using SharpCompress.Common;
using SharpCompress.Common.Rar;
using SharpCompress.Common.Rar.Headers;
using SharpCompress.Compressors.Rar;
using SharpCompress.Readers;

namespace SharpCompress.Archives.Rar;

public partial class RarArchiveEntry
{
    public async ValueTask<Stream> OpenEntryStreamAsync(
        CancellationToken cancellationToken = default
    )
    {
        RarStream stream;
        if (IsRarV3)
        {
            stream = new RarStream(
                archive.UnpackV1.Value,
                FileHeader,
                await MultiVolumeReadOnlyAsyncStream
                    .Create(Parts.ToAsyncEnumerable().CastAsync<RarFilePart>())
                    .ConfigureAwait(false)
            );
        }
        else
        {
            stream = new RarStream(
                archive.UnpackV2017.Value,
                FileHeader,
                await MultiVolumeReadOnlyAsyncStream
                    .Create(Parts.ToAsyncEnumerable().CastAsync<RarFilePart>())
                    .ConfigureAwait(false)
            );
        }

        await stream.InitializeAsync(cancellationToken).ConfigureAwait(false);
        return stream;
    }
}
