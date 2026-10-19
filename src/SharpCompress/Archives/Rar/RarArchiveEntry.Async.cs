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
        var readStream = await MultiVolumeReadOnlyAsyncStream
            .Create(Parts.ToAsyncEnumerable().CastAsync<RarFilePart>())
            .ConfigureAwait(false);
        RarStream stream;
        if (IsRarV3)
        {
            stream = await RarCrcStream
                .CreateAsync(archive.UnpackV1.Value, FileHeader, readStream, cancellationToken)
                .ConfigureAwait(false);
        }
        else if (FileHeader.FileCrc?.Length > 5)
        {
            stream = await RarBLAKE2spStream
                .CreateAsync(archive.UnpackV2017.Value, FileHeader, readStream, cancellationToken)
                .ConfigureAwait(false);
        }
        else
        {
            stream = await RarCrcStream
                .CreateAsync(archive.UnpackV2017.Value, FileHeader, readStream, cancellationToken)
                .ConfigureAwait(false);
        }

        try
        {
            await stream.InitializeAsync(cancellationToken).ConfigureAwait(false);
            return stream;
        }
        catch
        {
            // Initialization can fail before the caller can dispose the pooled stream buffers.
            await stream.DisposeAsyncCompat().ConfigureAwait(false);
            throw;
        }
    }
}
