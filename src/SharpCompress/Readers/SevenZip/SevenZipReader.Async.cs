using System.Threading;
using System.Threading.Tasks;
using SharpCompress.Common;

namespace SharpCompress.Readers.SevenZip;

public partial class SevenZipReader
{
    protected override ValueTask<EntryStream> GetEntryStreamAsync(
        CancellationToken cancellationToken = default
    ) => new(GetEntryStream());

    public override async ValueTask DisposeAsync()
    {
        _currentFolderStream?.Dispose();
        _currentFolderStream = null;
        await base.DisposeAsync().ConfigureAwait(false);
        if (_disposeArchive)
        {
            await _archive.DisposeAsync().ConfigureAwait(false);
        }
    }
}
