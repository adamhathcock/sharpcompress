using System.IO;
using System.Threading;
using System.Threading.Tasks;
using SharpCompress.Archives;
using SharpCompress.Common;

namespace SharpCompress.Services;

internal sealed partial class ArchiveEntryExtractor
{
    [Zomp.SyncMethodGenerator.CreateSyncVersion]
    public async ValueTask WriteToDirectoryAsync(
        IArchiveEntry entry,
        string destinationDirectory,
        ExtractionOptions? options = null,
        CancellationToken cancellationToken = default
    ) =>
        await entry
            .WriteEntryToDirectoryAsync(
                destinationDirectory,
                options,
                async (path, ct) =>
                    await WriteToFileAsync(entry, path, options, ct).ConfigureAwait(false),
                cancellationToken
            )
            .ConfigureAwait(false);

    [Zomp.SyncMethodGenerator.CreateSyncVersion]
    public async ValueTask WriteToFileAsync(
        IArchiveEntry entry,
        string destinationFileName,
        ExtractionOptions? options = null,
        CancellationToken cancellationToken = default
    )
    {
        options ??= new ExtractionOptions();
        await entry
            .WriteEntryToFileAsync(
                destinationFileName,
                options,
                async (x, fm, ct) =>
                {
#if SYNC_ONLY
                    using var fs = File.Open(x, fm);
#else
                    var fs = File.Open(x, fm);
                    await using var scope = fs.DisposeAsyncScope().ConfigureAwait(false);
#endif
                    await WriteToAsync(entry, fs, options.BufferSize, options, null, ct)
                        .ConfigureAwait(false);
                },
                cancellationToken
            )
            .ConfigureAwait(false);
    }
}
