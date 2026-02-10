using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using SharpCompress.Common;
using SharpCompress.Common.SevenZip;
using SharpCompress.IO;
using SharpCompress.Readers;

namespace SharpCompress.Archives.SevenZip;

public partial class SevenZipArchive
{
    private async ValueTask LoadFactoryAsync(
        Stream stream,
        CancellationToken cancellationToken = default
    )
    {
        if (_database is null)
        {
            stream.Position = 0;
            var reader = new ArchiveReader();
            await reader.OpenAsync(
                stream,
                lookForHeader: ReaderOptions.LookForHeader,
                cancellationToken
            );
            _database = await reader.ReadDatabaseAsync(
                new PasswordProvider(ReaderOptions.Password),
                cancellationToken
            );
        }
    }

    protected override async IAsyncEnumerable<SevenZipArchiveEntry> LoadEntriesAsync(
        IAsyncEnumerable<SevenZipVolume> volumes
    )
    {
        var stream = (await volumes.SingleAsync()).Stream;
        await LoadFactoryAsync(stream);
        if (_database is null)
        {
            yield break;
        }
        var entries = new SevenZipArchiveEntry[_database._files.Count];
        for (var i = 0; i < _database._files.Count; i++)
        {
            var file = _database._files[i];
            entries[i] = new SevenZipArchiveEntry(
                this,
                new SevenZipFilePart(stream, _database, i, file, ReaderOptions.ArchiveEncoding),
                ReaderOptions
            );
        }
        foreach (var group in entries.Where(x => !x.IsDirectory).GroupBy(x => x.FilePart.Folder))
        {
            var isSolid = false;
            foreach (var entry in group)
            {
                entry.IsSolid = isSolid;
                isSolid = true;
            }
        }

        foreach (var entry in entries)
        {
            yield return entry;
        }
    }

    protected override ValueTask<IAsyncReader> CreateReaderForSolidExtractionAsync() =>
        new(new SevenZipReader(ReaderOptions, this));
}
